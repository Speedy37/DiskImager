// This is the main DLL file.

#include "stdafx.h"

#include "liblzma_wrapper.h"


/// Information about a .xz file
typedef struct {
	/// Combined Index of all Streams in the file
	lzma_index *idx;

	/// Total amount of Stream Padding
	uint64_t stream_padding;

	/// Highest memory usage so far
	uint64_t memusage_max;

	/// True if all Blocks so far have Compressed Size and
	/// Uncompressed Size fields
	bool all_have_sizes;

	/// Oldest XZ Utils version that will decompress the file
	uint32_t min_version;

} xz_file_info;

static String^ message_strm(lzma_ret code)
{
	switch (code) {
	case LZMA_NO_CHECK:
		return ("No integrity check; not verifying file integrity");

	case LZMA_UNSUPPORTED_CHECK:
		return ("Unsupported type of integrity check; "
			"not verifying file integrity");

	case LZMA_MEM_ERROR:
		return ("Memory error");

	case LZMA_MEMLIMIT_ERROR:
		return ("Memory usage limit reached");

	case LZMA_FORMAT_ERROR:
		return ("File format not recognized");

	case LZMA_OPTIONS_ERROR:
		return ("Unsupported options");

	case LZMA_DATA_ERROR:
		return ("Compressed data is corrupt");

	case LZMA_BUF_ERROR:
		return ("Unexpected end of input");

	case LZMA_OK:
	case LZMA_STREAM_END:
	case LZMA_GET_CHECK:
	case LZMA_PROG_ERROR:
		// Without "default", compiler will warn if new constants
		// are added to lzma_ret, it is not too easy to forget to
		// add the new constants to this function.
		break;
	}

	return ("Internal error (bug)");
}

static bool io_pread(Stream ^stream, array<Byte>^buf, size_t size, int64_t pos)
{
	if (stream->Seek(pos, SeekOrigin::Begin) != pos)
		throw gcnew InvalidDataException("unable to seek to pos");
	if (stream->Read(buf, 0, size) != size)
		throw gcnew InvalidDataException("unable to read size");
	return true;
}

struct xz_file_basic_info
{
	uint64_t stream_count;
	uint64_t block_count;
	uint64_t file_size;
	uint64_t uncompressed_size;
};

static void liblzma_wrapper::parse_indexes(liblzma_wrapper::XZFileInfo ^xfbi, Stream ^stream)
{
	xz_file_info sxfi = { NULL, 0, 0, true, 50000002 };
	xz_file_info *xfi = &sxfi;
	int64_t length = stream->Length;
	if (length < 2 * LZMA_STREAM_HEADER_SIZE)
		throw gcnew InvalidDataException("Too small to be a valid .xz file");

	lzma_stream_flags header_flags;
	lzma_stream_flags footer_flags;
	lzma_ret ret;

	// lzma_stream for the Index decoder
	lzma_stream strm = LZMA_STREAM_INIT;

	// All Indexes decoded so far
	lzma_index *combined_index = NULL;

	// The Index currently being decoded
	lzma_index *this_index = NULL;

	try {


		// Current position in the file. We parse the file backwards so
		// initialize it to point to the end of the file.
		int64_t pos = length;
		array<Byte>^ nums = gcnew array<Byte>(IO_BUFFER_SIZE);
		pin_ptr<Byte> pp = &nums[0];
		uint8_t *buf_u8 = pp;
		uint32_t *buf_u32 = (uint32_t *)(buf_u8);

		// Each loop iteration decodes one Index.
		do {
			// Check that there is enough data left to contain at least
			// the Stream Header and Stream Footer. This check cannot
			// fail in the first pass of this loop.
			if (pos < 2 * LZMA_STREAM_HEADER_SIZE)
				throw gcnew InvalidDataException(message_strm(LZMA_DATA_ERROR));

			pos -= LZMA_STREAM_HEADER_SIZE;
			lzma_vli stream_padding = 0;

			// Locate the Stream Footer. There may be Stream Padding which
			// we must skip when reading backwards.
			while (true) {
				if (pos < LZMA_STREAM_HEADER_SIZE)
					throw gcnew InvalidDataException(message_strm(LZMA_DATA_ERROR));

				io_pread(stream, nums, LZMA_STREAM_HEADER_SIZE, pos);

				// Stream Padding is always a multiple of four bytes.
				int i = 2;
				if (buf_u32[i] != 0)
					break;

				// To avoid calling io_pread() for every four bytes
				// of Stream Padding, take advantage that we read
				// 12 bytes (LZMA_STREAM_HEADER_SIZE) already and
				// check them too before calling io_pread() again.
				do {
					stream_padding += 4;
					pos -= 4;
					--i;
				} while (i >= 0 && buf_u32[i] == 0);
			}

			// Decode the Stream Footer.
			ret = lzma_stream_footer_decode(&footer_flags, buf_u8);
			if (ret != LZMA_OK)
				throw gcnew InvalidDataException(message_strm(ret));

			// Check that the Stream Footer doesn't specify something
			// that we don't support. This can only happen if the xz
			// version is older than liblzma and liblzma supports
			// something new.
			//
			// It is enough to check Stream Footer. Stream Header must
			// match when it is compared against Stream Footer with
			// lzma_stream_flags_compare().
			if (footer_flags.version != 0)
				throw gcnew InvalidDataException(message_strm(LZMA_OPTIONS_ERROR));

			// Check that the size of the Index field looks sane.
			lzma_vli index_size = footer_flags.backward_size;
			if ((lzma_vli)(pos) < index_size + LZMA_STREAM_HEADER_SIZE)
				throw gcnew InvalidDataException(message_strm(LZMA_DATA_ERROR));

			// Set pos to the beginning of the Index.
			pos -= index_size;

			// Decode the Index.
			ret = lzma_index_decoder(&strm, &this_index, UINT64_MAX);
			if (ret != LZMA_OK)
				throw gcnew InvalidDataException(message_strm(ret));

			do {
				// Don't give the decoder more input than the
				// Index size.
				strm.avail_in = (size_t)(IO_BUFFER_SIZE < index_size ? IO_BUFFER_SIZE : index_size);
				io_pread(stream, nums, strm.avail_in, pos);

				pos += strm.avail_in;
				index_size -= strm.avail_in;

				strm.next_in = buf_u8;
				ret = lzma_code(&strm, LZMA_RUN);

			} while (ret == LZMA_OK);

			// If the decoding seems to be successful, check also that
			// the Index decoder consumed as much input as indicated
			// by the Backward Size field.
			if (ret == LZMA_STREAM_END)
				if (index_size != 0 || strm.avail_in != 0)
					ret = LZMA_DATA_ERROR;

			if (ret != LZMA_STREAM_END) {
				// LZMA_BUFFER_ERROR means that the Index decoder
				// would have liked more input than what the Index
				// size should be according to Stream Footer.
				// The message for LZMA_DATA_ERROR makes more
				// sense in that case.
				if (ret == LZMA_BUF_ERROR)
					ret = LZMA_DATA_ERROR;

				throw gcnew InvalidDataException(message_strm(ret));
			}

			// Decode the Stream Header and check that its Stream Flags
			// match the Stream Footer.
			pos -= footer_flags.backward_size + LZMA_STREAM_HEADER_SIZE;
			if ((lzma_vli)(pos) < lzma_index_total_size(this_index))
				throw gcnew InvalidDataException(message_strm(LZMA_DATA_ERROR));

			pos -= lzma_index_total_size(this_index);
			io_pread(stream, nums, LZMA_STREAM_HEADER_SIZE, pos);

			ret = lzma_stream_header_decode(&header_flags, buf_u8);
			if (ret != LZMA_OK)
				throw gcnew InvalidDataException(message_strm(ret));

			ret = lzma_stream_flags_compare(&header_flags, &footer_flags);
			if (ret != LZMA_OK)
				throw gcnew InvalidDataException(message_strm(ret));

			// Store the decoded Stream Flags into this_index. This is
			// needed so that we can print which Check is used in each
			// Stream.
			ret = lzma_index_stream_flags(this_index, &footer_flags);
			if (ret != LZMA_OK)
				throw gcnew InvalidOperationException(message_strm(ret));

			// Store also the size of the Stream Padding field. It is
			// needed to show the offsets of the Streams correctly.
			ret = lzma_index_stream_padding(this_index, stream_padding);
			if (ret != LZMA_OK)
				throw gcnew InvalidOperationException(message_strm(ret));

			if (combined_index != NULL) {
				// Append the earlier decoded Indexes
				// after this_index.
				ret = lzma_index_cat(
					this_index, combined_index, NULL);
				if (ret != LZMA_OK)
					throw gcnew InvalidOperationException(message_strm(ret));
			}

			combined_index = this_index;
			this_index = NULL;

			xfi->stream_padding += stream_padding;

		} while (pos > 0);

		// All OK. Make combined_index available to the caller.
		xfi->idx = combined_index;


		xfbi->stream_count = lzma_index_stream_count(xfi->idx);
		xfbi->block_count = lzma_index_block_count(xfi->idx);
		xfbi->file_size = lzma_index_file_size(xfi->idx);
		xfbi->uncompressed_size = lzma_index_uncompressed_size(xfi->idx);
	}
	finally
	{
		lzma_end(&strm);
		lzma_index_end(combined_index, NULL);
		lzma_index_end(this_index, NULL);
	}
}

int liblzma_wrapper::LZMAStream::Read(cli::array<unsigned char, 1> ^buffer, int offset, int size)
{
	if (bCompress)
		throw gcnew InvalidOperationException("Operation not supported");

	if (strm->avail_in == 0)
	{
		strm->avail_in = stream->Read(gc_buf, 0, gc_buf->Length);
		pin_ptr<Byte> pp_inbuf = &gc_buf[0];
		strm->next_in = pp_inbuf;
	}


	pin_ptr<Byte> pp_outbuf = &buffer[0];
	strm->next_out = pp_outbuf + offset;
	strm->avail_out = size;

	ret = lzma_code(strm, LZMA_RUN);

	if (ret != LZMA_OK && ret != LZMA_STREAM_END)
		throw gcnew InvalidOperationException(message_strm(ret));

	return size - strm->avail_out;
}

void liblzma_wrapper::LZMAStream::Flush()
{
	if (bCompress)
	{
		do
		{
			strm->next_in = nullptr;
			strm->avail_in = 0;

			pin_ptr<Byte> pp_outbuf = &gc_buf[0];
			strm->next_out = pp_outbuf;
			size_t outlen = gc_buf->Length;
			strm->avail_out = outlen;

			ret = lzma_code(strm, LZMA_FINISH);

			if (ret != LZMA_OK && ret != LZMA_STREAM_END)
				throw gcnew InvalidOperationException(message_strm(ret));

			int64_t avail = outlen - strm->avail_out;
			if (avail > 0)
				stream->Write(gc_buf, 0, avail);
		} while (strm->avail_in > 0);
	}
}

void liblzma_wrapper::LZMAStream::Write(cli::array<unsigned char, 1> ^buffer, int offset, int size)
{
	if (!bCompress)
		throw gcnew InvalidOperationException("Operation not supported");

	pin_ptr<Byte> pp_inbuf = &buffer[0];
	strm->next_in = pp_inbuf + offset;
	strm->avail_in = size;

	while (strm->avail_in > 0)
	{
		pin_ptr<Byte> pp_outbuf = &gc_buf[0];
		strm->next_out = pp_outbuf;
		size_t outlen = gc_buf->Length;
		strm->avail_out = outlen;

		ret = lzma_code(strm, LZMA_RUN);

		if (ret != LZMA_OK && ret != LZMA_STREAM_END)
			throw gcnew InvalidOperationException(message_strm(ret));

		int64_t avail = outlen - strm->avail_out;
		if (avail > 0)
			stream->Write(gc_buf, 0, avail);
	}
}
