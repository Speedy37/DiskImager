// liblzma_wrapper.h

#pragma once
#include "lzma.h"

#if BUFSIZ <= 1024
#	define IO_BUFFER_SIZE 8192
#else
#	define IO_BUFFER_SIZE (BUFSIZ & ~7U)
#endif

using namespace System;
using namespace System::IO;

namespace liblzma_wrapper {
	ref struct XZFileInfo;
	static void parse_indexes(XZFileInfo ^xfbi, Stream ^stream);

	public ref struct XZFileInfo {
		XZFileInfo(Stream^ src)
		{
			parse_indexes(this, src);
		}
		uint64_t stream_count;
		uint64_t block_count;
		uint64_t file_size;
		uint64_t uncompressed_size;
	};

	public ref class LZMAStream : public Stream
	{
	private:
		Stream ^stream;
		lzma_stream *strm;
		lzma_ret ret;
		array<Byte>^ gc_buf;
		bool bCompress;
		uint64_t offset;
	public:
		LZMAStream(Stream^ stream, bool bCompress)
		{
			lzma_stream strm = LZMA_STREAM_INIT;
			this->stream = stream;
			this->bCompress = bCompress;
			this->gc_buf = gcnew array<Byte>(IO_BUFFER_SIZE);

			this->offset = 0;
			this->strm = new lzma_stream();
			*this->strm = strm;
			if (bCompress)
			{
				this->ret = lzma_easy_encoder(this->strm, LZMA_PRESET_DEFAULT, LZMA_CHECK_SHA256);
			}
			else
			{
				this->ret = lzma_stream_decoder(this->strm, UINT64_MAX, LZMA_CONCATENATED);
			}
			this->strm->avail_in = 0;
		}
		~LZMAStream()
		{
			if (strm) {
				lzma_end(strm);
				delete strm;
			}
			if (stream) {
				stream->~Stream();
			}
		}

		property bool CanRead { bool get() override { return !bCompress; } }
		property bool CanSeek { bool get() override { return false; } }
		property bool CanWrite { bool get() override { return bCompress; } }
		property int64_t Length { int64_t get() override { throw gcnew InvalidOperationException("Operation not supported"); } }
		property int64_t Position {
			int64_t get() override { throw gcnew InvalidOperationException("Operation not supported"); }
			void set(int64_t value) override  { throw gcnew InvalidOperationException("Operation not supported"); }
		}
		void Flush() override;
		int64_t Seek(int64_t pos, SeekOrigin origin) override { throw gcnew InvalidOperationException("Operation not supported"); }
		void SetLength(int64_t) override { throw gcnew InvalidOperationException("Operation not supported"); }
		int Read(cli::array<unsigned char, 1> ^buffer, int offset, int size) override;
		void Write(cli::array<unsigned char, 1> ^buffer, int offset, int size) override;

	};
}
