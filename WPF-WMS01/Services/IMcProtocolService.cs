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
        /// <param name="ipAddress">PLC의 IP 주소.</param>
        /// <param name="port">PLC의 포트 번호 (선택 사항, 기본값 사용 시 null).</param>
        /// <returns>연결 성공 여부.</returns>
        Task<bool> ConnectAsync(string ipAddress, int? port = null);

        /// <summary>
        /// PLC 연결을 해제합니다.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// PLC 연결 상태를 가져옵니다.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 현재 연결된 PLC의 IP 주소를 가져옵니다.
        /// </summary>
        string ConnectedIpAddress { get; }

        // 기존의 ReadBitAsync, WriteBitAsync는 MC Protocol이 Word 단위로 동작하므로 제거합니다.
        // 대신 ReadWordAsync, WriteWordAsync를 사용하여 비트 상태를 워드 값으로 처리합니다.

        /// <summary>
        /// PLC의 워드 디바이스(예: D, W, R) 값을 읽습니다. (단일 워드)
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 워드 값.</returns>
        Task<ushort> ReadWordAsync(string deviceCode, int address);

        /// <summary>
        /// PLC의 워드 디바이스(예: D, W, R) 값을 씁니다. (단일 워드)
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 워드 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteWordAsync(string deviceCode, int address, ushort value);

        /// <summary>
        /// PLC의 워드 디바이스에서 여러 워드 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="startAddress">시작 디바이스 주소.</param>
        /// <param name="numberOfWords">읽을 워드 개수.</param>
        /// <returns>읽은 워드 값 배열.</returns>
        Task<ushort[]> ReadWordsAsync(string deviceCode, int startAddress, ushort numberOfWords);

        /// <summary>
        /// PLC의 워드 디바이스에 여러 워드 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="startAddress">시작 디바이스 주소.</param>
        /// <param name="values">쓸 워드 값 배열.</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteWordsAsync(string deviceCode, int startAddress, ushort[] values);

        /// <summary>
        /// PLC의 워드 디바이스에서 20바이트(10워드) 문자열을 읽습니다.
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <returns>읽은 문자열.</returns>
        Task<string> ReadStringDataAsync(string deviceCode, int address);

        /// <summary>
        /// PLC의 워드 디바이스에 20바이트(10워드) 문자열을 씁니다.
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <param name="value">쓸 문자열 (20바이트로 패딩/잘림).</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteStringDataAsync(string deviceCode, int address, string value);

        /// <summary>
        /// PLC의 워드 디바이스에서 32비트 정수(int)를 읽습니다. (2워드)
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <returns>읽은 정수 값.</returns>
        Task<int> ReadIntDataAsync(string deviceCode, int address);

        /// <summary>
        /// PLC의 워드 디바이스에 32비트 정수(int)를 씁니다. (2워드)
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <param name="value">쓸 정수 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        Task<bool> WriteIntDataAsync(string deviceCode, int address, int value);
    }
}
