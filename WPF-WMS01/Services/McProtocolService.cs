// Services/McProtocolService.cs
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq; // For byte array manipulation

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Mitsubishi MC Protocol 통신을 위한 서비스 구현체입니다.
    /// QnA 형식 3E 프레임을 가정하며, 실제 PLC 통신을 위한 패킷 구성은 더 정교해야 합니다.
    /// </summary>
    public class McProtocolService : IMcProtocolService
    {
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly byte _cpuType; // CPU 타입 (예: 0x90 for QCPU)
        private readonly byte _networkNo; // 네트워크 번호 (기본 0x00)
        private readonly byte _pcNo; // PC 번호 (기본 0xFF)

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isConnected;
        private readonly object _lock = new object(); // 통신 중복 방지 락

        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;

        /// <summary>
        /// McProtocolService의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="ipAddress">PLC의 IP 주소.</param>
        /// <param name="port">PLC의 포트 번호 (일반적으로 5000).</param>
        /// <param name="cpuType">PLC CPU 타입 (예: 0x90 for QCPU). Hex 값으로 전달.</param>
        /// <param name="networkNo">네트워크 번호 (기본 0x00).</param>
        /// <param name="pcNo">PC 번호 (기본 0xFF).</param>
        public McProtocolService(string ipAddress, int port, byte cpuType, byte networkNo, byte pcNo)
        {
            _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            _port = port;
            _cpuType = cpuType;
            _networkNo = networkNo;
            _pcNo = pcNo;
            Debug.WriteLine($"[McProtocolService] Initialized with IP: {_ipAddress}, Port: {_port}, CPU: 0x{_cpuType:X2}, Net: 0x{_networkNo:X2}, PC: 0x{_pcNo:X2}");
        }

        /// <summary>
        /// PLC에 연결합니다.
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Already connected.");
                return true;
            }

            lock (_lock) // 연결 시도 중 다른 연결 시도 방지
            {
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
                _tcpClient = new TcpClient();
            }

            try
            {
                Debug.WriteLine($"[McProtocolService] Attempting to connect to {_ipAddress}:{_port}...");
                await _tcpClient.ConnectAsync(_ipAddress, _port).ConfigureAwait(false);
                _networkStream = _tcpClient.GetStream();
                _isConnected = true;
                Debug.WriteLine($"[McProtocolService] Successfully connected to {_ipAddress}:{_port}.");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Debug.WriteLine($"[McProtocolService] Connection failed: {ex.Message}");
                Dispose(); // 연결 실패 시 자원 정리
                return false;
            }
        }

        /// <summary>
        /// PLC 연결을 해제합니다.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected) return;

            lock (_lock)
            {
                try
                {
                    _networkStream?.Close();
                    _networkStream?.Dispose();
                    _tcpClient?.Close();
                    _tcpClient?.Dispose();
                    _isConnected = false;
                    Debug.WriteLine("[McProtocolService] Disconnected.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[McProtocolService] Error during disconnect: {ex.Message}");
                }
                finally
                {
                    _networkStream = null;
                    _tcpClient = null;
                }
            }
        }

        /// <summary>
        /// MC Protocol 3E 프레임 헤더를 생성합니다.
        /// </summary>
        /// <param name="dataLength">데이터 부분의 길이 (명령어 + 서브헤더 + 데이터).</param>
        /// <returns>MC Protocol 헤더 바이트 배열.</returns>
        private byte[] CreateMcProtocolHeader(ushort dataLength)
        {
            // Subheader (2 bytes): 0x5000 (Fixed)
            // Network No (1 byte): 0x00 (Fixed, or configurable)
            // PC No (1 byte): 0xFF (Fixed, or configurable)
            // Request Destination Module I/O No (2 bytes): 0x03FF (Fixed)
            // Request Destination Module Station No (1 byte): 0x00 (Fixed)
            // Request Data Length (2 bytes): Length of command + sub-command + data
            // Monitoring Timer (2 bytes): 0x0010 (100ms * 16 = 1.6s)

            byte[] header = new byte[11];
            header[0] = 0x50; // Subheader Low
            header[1] = 0x00; // Subheader High
            header[2] = _networkNo; // Network No
            header[3] = _pcNo; // PC No
            header[4] = 0xFF; // Request Destination Module I/O No Low
            header[5] = 0x03; // Request Destination Module I/O No High
            header[6] = 0x00; // Request Destination Module Station No
            header[7] = (byte)(dataLength & 0xFF); // Request Data Length Low
            header[8] = (byte)((dataLength >> 8) & 0xFF); // Request Data Length High
            header[9] = 0x10; // Monitoring Timer Low (100ms * 16 = 1.6s)
            header[10] = 0x00; // Monitoring Timer High

            return header;
        }

        /// <summary>
        /// MC Protocol 응답 헤더를 파싱하고 오류를 확인합니다.
        /// </summary>
        /// <param name="responseBytes">PLC로부터 받은 전체 응답 바이트 배열.</param>
        /// <returns>데이터 부분의 길이.</returns>
        /// <exception cref="Exception">MC Protocol 통신 오류 발생 시.</exception>
        private ushort ParseMcProtocolResponseHeader(byte[] responseBytes)
        {
            if (responseBytes.Length < 11)
            {
                throw new Exception("MC Protocol 응답 헤더가 너무 짧습니다.");
            }

            // Subheader (2 bytes): 0xD000 (Fixed for response)
            // Network No (1 byte)
            // PC No (1 byte)
            // Response Destination Module I/O No (2 bytes)
            // Response Destination Module Station No (1 byte)
            // Response Data Length (2 bytes): Length of completion code + data
            // Completion Code (2 bytes): 0x0000 for success

            ushort completionCode = (ushort)(responseBytes[9] | (responseBytes[10] << 8));
            if (completionCode != 0x0000)
            {
                // MC Protocol 에러 코드 처리
                string errorMessage = $"MC Protocol 오류 발생. 완료 코드: 0x{completionCode:X4}";
                // 실제 구현에서는 completionCode에 따라 더 상세한 에러 메시지를 제공할 수 있습니다.
                throw new Exception(errorMessage);
            }

            ushort dataLength = (ushort)(responseBytes[7] | (responseBytes[8] << 8));
            return dataLength;
        }

        /// <summary>
        /// PLC의 비트 디바이스 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "M", "D").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 비트 값.</returns>
        public async Task<bool> ReadBitAsync(string deviceCode, int address)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for ReadBitAsync.");
                if (!await ConnectAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("MC Protocol PLC에 연결되어 있지 않습니다.");
                }
            }

            // Command (2 bytes): 0x0401 (Batch Read)
            // Sub-command (2 bytes): 0x0001 (Bit Read)
            // Device Code (1 byte) + Device Address (3 bytes)
            // Number of Devices (2 bytes)

            byte[] command = new byte[10];
            command[0] = 0x01; // Command Low (Batch Read)
            command[1] = 0x04; // Command High
            command[2] = 0x01; // Sub-command Low (Bit Read)
            command[3] = 0x00; // Sub-command High

            // Device Address (3 bytes, Little Endian)
            command[4] = (byte)(address & 0xFF);
            command[5] = (byte)((address >> 8) & 0xFF);
            command[6] = (byte)((address >> 16) & 0xFF);

            // Device Code (1 byte) - 예시: M=0x90, D=0xA8, X=0x9C, Y=0x9D
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes) - 1 bit
            command[8] = 0x01;
            command[9] = 0x00;

            ushort dataLength = (ushort)command.Length;
            byte[] header = CreateMcProtocolHeader(dataLength);
            byte[] request = header.Concat(command).ToArray();

            try
            {
                await _networkStream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                byte[] responseHeader = new byte[11];
                int bytesRead = await _networkStream.ReadAsync(responseHeader, 0, responseHeader.Length).ConfigureAwait(false);
                if (bytesRead != responseHeader.Length) throw new Exception("MC Protocol 응답 헤더 읽기 실패.");

                ushort responseDataLength = ParseMcProtocolResponseHeader(responseHeader);

                byte[] responseData = new byte[responseDataLength - 2]; // -2 for completion code
                bytesRead = await _networkStream.ReadAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
                if (bytesRead != responseData.Length) throw new Exception("MC Protocol 응답 데이터 읽기 실패.");

                // 비트 응답은 0x00 (OFF) 또는 0x01 (ON)으로 옵니다.
                if (responseData.Length > 0)
                {
                    return responseData[0] == 0x01;
                }
                throw new Exception("MC Protocol 비트 읽기 응답 데이터 없음.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] ReadBitAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PLC의 비트 디바이스 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "M", "D").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 비트 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteBitAsync(string deviceCode, int address, bool value)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for WriteBitAsync.");
                if (!await ConnectAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("MC Protocol PLC에 연결되어 있지 않습니다.");
                }
            }

            // Command (2 bytes): 0x0402 (Batch Write)
            // Sub-command (2 bytes): 0x0001 (Bit Write)
            // Device Code (1 byte) + Device Address (3 bytes)
            // Number of Devices (2 bytes)
            // Write Data (N bytes)

            byte[] command = new byte[11];
            command[0] = 0x02; // Command Low (Batch Write)
            command[1] = 0x04; // Command High
            command[2] = 0x01; // Sub-command Low (Bit Write)
            command[3] = 0x00; // Sub-command High

            // Device Address (3 bytes, Little Endian)
            command[4] = (byte)(address & 0xFF);
            command[5] = (byte)((address >> 8) & 0xFF);
            command[6] = (byte)((address >> 16) & 0xFF);

            // Device Code (1 byte)
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes) - 1 bit
            command[8] = 0x01;
            command[9] = 0x00;

            // Write Data (1 byte for 1 bit)
            command[10] = value ? (byte)0x01 : (byte)0x00;

            ushort dataLength = (ushort)command.Length;
            byte[] header = CreateMcProtocolHeader(dataLength);
            byte[] request = header.Concat(command).ToArray();

            try
            {
                await _networkStream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                byte[] responseHeader = new byte[11];
                int bytesRead = await _networkStream.ReadAsync(responseHeader, 0, responseHeader.Length).ConfigureAwait(false);
                if (bytesRead != responseHeader.Length) throw new Exception("MC Protocol 응답 헤더 읽기 실패.");

                ParseMcProtocolResponseHeader(responseHeader); // 오류 확인

                // 쓰기 응답은 성공 시 헤더만 오고 데이터는 없습니다.
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] WriteBitAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 워드 값.</returns>
        public async Task<ushort> ReadWordAsync(string deviceCode, int address)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for ReadWordAsync.");
                if (!await ConnectAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("MC Protocol PLC에 연결되어 있지 않습니다.");
                }
            }

            // Command (2 bytes): 0x0401 (Batch Read)
            // Sub-command (2 bytes): 0x0000 (Word Read)
            // Device Code (1 byte) + Device Address (3 bytes)
            // Number of Devices (2 bytes)

            byte[] command = new byte[10];
            command[0] = 0x01; // Command Low (Batch Read)
            command[1] = 0x04; // Command High
            command[2] = 0x00; // Sub-command Low (Word Read)
            command[3] = 0x00; // Sub-command High

            // Device Address (3 bytes, Little Endian)
            command[4] = (byte)(address & 0xFF);
            command[5] = (byte)((address >> 8) & 0xFF);
            command[6] = (byte)((address >> 16) & 0xFF);

            // Device Code (1 byte)
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes) - 1 word
            command[8] = 0x01;
            command[9] = 0x00;

            ushort dataLength = (ushort)command.Length;
            byte[] header = CreateMcProtocolHeader(dataLength);
            byte[] request = header.Concat(command).ToArray();

            try
            {
                await _networkStream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                byte[] responseHeader = new byte[11];
                int bytesRead = await _networkStream.ReadAsync(responseHeader, 0, responseHeader.Length).ConfigureAwait(false);
                if (bytesRead != responseHeader.Length) throw new Exception("MC Protocol 응답 헤더 읽기 실패.");

                ushort responseDataLength = ParseMcProtocolResponseHeader(responseHeader);

                byte[] responseData = new byte[responseDataLength - 2]; // -2 for completion code
                bytesRead = await _networkStream.ReadAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
                if (bytesRead != responseData.Length) throw new Exception("MC Protocol 응답 데이터 읽기 실패.");

                // 워드 응답은 2바이트 (Little Endian)
                if (responseData.Length >= 2)
                {
                    return (ushort)(responseData[0] | (responseData[1] << 8));
                }
                throw new Exception("MC Protocol 워드 읽기 응답 데이터 없음.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] ReadWordAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 워드 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteWordAsync(string deviceCode, int address, ushort value)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for WriteWordAsync.");
                if (!await ConnectAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("MC Protocol PLC에 연결되어 있지 않습니다.");
                }
            }

            // Command (2 bytes): 0x0402 (Batch Write)
            // Sub-command (2 bytes): 0x0000 (Word Write)
            // Device Code (1 byte) + Device Address (3 bytes)
            // Number of Devices (2 bytes)
            // Write Data (2 bytes)

            byte[] command = new byte[12];
            command[0] = 0x02; // Command Low (Batch Write)
            command[1] = 0x04; // Command High
            command[2] = 0x00; // Sub-command Low (Word Write)
            command[3] = 0x00; // Sub-command High

            // Device Address (3 bytes, Little Endian)
            command[4] = (byte)(address & 0xFF);
            command[5] = (byte)((address >> 8) & 0xFF);
            command[6] = (byte)((address >> 16) & 0xFF);

            // Device Code (1 byte)
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes) - 1 word
            command[8] = 0x01;
            command[9] = 0x00;

            // Write Data (2 bytes, Little Endian)
            command[10] = (byte)(value & 0xFF);
            command[11] = (byte)((value >> 8) & 0xFF);

            ushort dataLength = (ushort)command.Length;
            byte[] header = CreateMcProtocolHeader(dataLength);
            byte[] request = header.Concat(command).ToArray();

            try
            {
                await _networkStream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);

                byte[] responseHeader = new byte[11];
                int bytesRead = await _networkStream.ReadAsync(responseHeader, 0, responseHeader.Length).ConfigureAwait(false);
                if (bytesRead != responseHeader.Length) throw new Exception("MC Protocol 응답 헤더 읽기 실패.");

                ParseMcProtocolResponseHeader(responseHeader); // 오류 확인

                // 쓰기 응답은 성공 시 헤더만 오고 데이터는 없습니다.
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] WriteWordAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 디바이스 코드 문자열에 해당하는 바이트 값을 반환합니다.
        /// 실제 PLC 스펙에 따라 정확한 코드 매핑이 필요합니다.
        /// </summary>
        private byte GetDeviceCodeByte(string deviceCode)
        {
            switch (deviceCode.ToUpper())
            {
                case "M": return 0x90;
                case "D": return 0xA8;
                case "X": return 0x9C;
                case "Y": return 0x9D;
                case "B": return 0xA0; // Buffer Memory
                case "W": return 0xB4; // Word Device
                case "R": return 0xAF; // File Register
                // 필요한 다른 디바이스 코드 추가
                default: throw new ArgumentException($"알 수 없는 MC Protocol 디바이스 코드: {deviceCode}");
            }
        }

        /// <summary>
        /// 서비스 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            Debug.WriteLine("[McProtocolService] Disposed.");
        }
    }
}
