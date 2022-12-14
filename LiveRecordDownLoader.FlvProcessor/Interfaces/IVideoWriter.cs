using LiveRecordDownLoader.FlvProcessor.Enums;
using LiveRecordDownLoader.FlvProcessor.Models;
using System;
using System.Threading.Tasks;

namespace LiveRecordDownLoader.FlvProcessor.Interfaces
{
	internal interface IVideoWriter
	{
		int BufferSize { get; init; }
		/// <summary>
		/// 输出时是否使用异步
		/// </summary>
		bool IsAsync { get; init; }
		string Path { get; }
		void Write(Memory<byte> buffer, uint timestamp, FrameType type);
		ValueTask FinishAsync(FractionUInt32 averageFrameRate);
	}
}
