// Services/IMcProtocolService.cs
using System;
using System.Threading.Tasks;

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Mitsubishi MC Protocol 통신을 위한 인터페이스입니다.
    /// </summary>
    public interface IMcProtocolService : IDisposable
    {
        /// <summary>
        /// PLC에 연결합니다.
        /// </summary>
        /// <returns>연결 성공 여부.</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// PLC 연결을 해제합니다.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// PLC 연결 상태를 가져옵니다.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// PLC의 비트 디바이스(예: X, Y, M, B) 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "M", "D").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 비트 값.</returns>
        Task<bool> ReadBitAsync(string deviceCode, int address);

        /// <summary>
        /// PLC의 비트 디바이스(예: X, Y, M, B) 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "M", "D").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 비트 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteBitAsync(string deviceCode, int address, bool value);

        /// <summary>
        /// PLC의 워드 디바이스(예: D, W, R) 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 워드 값.</returns>
        Task<ushort> ReadWordAsync(string deviceCode, int address);

        /// <summary>
        /// PLC의 워드 디바이스(예: D, W, R) 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 워드 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteWordAsync(string deviceCode, int address, ushort value);
    }
}
