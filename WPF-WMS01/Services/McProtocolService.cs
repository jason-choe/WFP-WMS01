// Services/McProtocolService.cs
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq; // For byte array manipulation
using System.Text; // For string encoding

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Mitsubishi MC Protocol 통신을 위한 서비스 구현체입니다.
    /// QnA 형식 3E 프레임을 가정하며, 실제 PLC 통신을 위한 패킷 구성은 더 정교해야 합니다.
    /// </summary>
    public class McProtocolService : IMcProtocolService
    {
        private string _ipAddress; // 초기화 시점에 설정되거나 ConnectAsync에서 변경될 수 있음
        private int _port; // 초기화 시점에 설정되거나 ConnectAsync에서 변경될 수 있음
        private readonly byte _cpuType; // CPU 타입 (예: 0x90 for QCPU)
        private readonly byte _networkNo; // 네트워크 번호 (기본 0x00)
        private readonly byte _pcNo; // PC 번호 (기본 0xFF)

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isConnected;
        private readonly object _lock = new object(); // 통신 중복 방지 락

        public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
        public string ConnectedIpAddress => _ipAddress; // 현재 연결된 IP 주소 반환

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
        /// <returns>연결 성공 여부.</returns>
        public async Task<bool> ConnectAsync()
        {
            return await ConnectAsync(_ipAddress, _port).ConfigureAwait(false);
        }

        /// <summary>
        /// 지정된 IP 주소와 포트로 PLC에 연결합니다.
        /// 기존 연결이 있다면 해제 후 새로 연결합니다.
        /// </summary>
        /// <param name="ipAddress">연결할 PLC의 IP 주소.</param>
        /// <param name="port">연결할 PLC의 포트 번호.</param>
        /// <returns>연결 성공 여부.</returns>
        public async Task<bool> ConnectAsync(string ipAddress, int? port = null)
        {
            // IP 주소나 포트가 변경되었다면 기존 연결 해제
            if (IsConnected && (_ipAddress != ipAddress || _port != (port ?? _port)))
            {
                Disconnect();
            }

            // 새로운 IP 주소와 포트 업데이트 (null이 아니면)
            if (ipAddress != null) _ipAddress = ipAddress;
            if (port.HasValue) _port = port.Value;

            if (IsConnected)
            {
                Debug.WriteLine($"[McProtocolService] Already connected to {_ipAddress}:{_port}.");
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

        // 기존의 ReadBitAsync, WriteBitAsync 메서드는 제거됩니다.

        /// <summary>
        /// PLC의 워드 디바이스에서 여러 워드 값을 읽습니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="startAddress">시작 디바이스 주소.</param>
        /// <param name="numberOfWords">읽을 워드 개수.</param>
        /// <returns>읽은 워드 값 배열.</returns>
        public async Task<ushort[]> ReadWordsAsync(string deviceCode, int startAddress, ushort numberOfWords)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for ReadWordsAsync.");
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
            command[4] = (byte)(startAddress & 0xFF);
            command[5] = (byte)((startAddress >> 8) & 0xFF);
            command[6] = (byte)((startAddress >> 16) & 0xFF);

            // Device Code (1 byte)
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes)
            command[8] = (byte)(numberOfWords & 0xFF);
            command[9] = (byte)((numberOfWords >> 8) & 0xFF);

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

                // 응답 데이터 길이 = 요청한 워드 수 * 2 바이트 (워드당 2바이트)
                byte[] responseData = new byte[responseDataLength - 2]; // -2 for completion code
                bytesRead = await _networkStream.ReadAsync(responseData, 0, responseData.Length).ConfigureAwait(false);
                if (bytesRead != responseData.Length) throw new Exception("MC Protocol 응답 데이터 읽기 실패.");

                if (responseData.Length != numberOfWords * 2)
                {
                    throw new Exception($"예상한 워드 데이터 길이({numberOfWords * 2})와 다릅니다. 실제: {responseData.Length}");
                }

                ushort[] resultWords = new ushort[numberOfWords];
                for (int i = 0; i < numberOfWords; i++)
                {
                    // Little Endian 바이트 순서로 워드 값 재구성
                    resultWords[i] = (ushort)(responseData[i * 2] | (responseData[i * 2 + 1] << 8));
                }
                return resultWords;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] ReadWordsAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스에 여러 워드 값을 씁니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="startAddress">시작 디바이스 주소.</param>
        /// <param name="values">쓸 워드 값 배열.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteWordsAsync(string deviceCode, int startAddress, ushort[] values)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[McProtocolService] Not connected. Attempting to reconnect for WriteWordsAsync.");
                if (!await ConnectAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("MC Protocol PLC에 연결되어 있지 않습니다.");
                }
            }

            // Command (2 bytes): 0x0402 (Batch Write)
            // Sub-command (2 bytes): 0x0000 (Word Write)
            // Device Code (1 byte) + Device Address (3 bytes)
            // Number of Devices (2 bytes)
            // Write Data (N bytes)

            byte[] command = new byte[10 + values.Length * 2]; // 10 bytes for command + header, 2 bytes per word
            command[0] = 0x02; // Command Low (Batch Write)
            command[1] = 0x04; // Command High
            command[2] = 0x00; // Sub-command Low (Word Write)
            command[3] = 0x00; // Sub-command High

            // Device Address (3 bytes, Little Endian)
            command[4] = (byte)(startAddress & 0xFF);
            command[5] = (byte)((startAddress >> 8) & 0xFF);
            command[6] = (byte)((startAddress >> 16) & 0xFF);

            // Device Code (1 byte)
            command[7] = GetDeviceCodeByte(deviceCode);

            // Number of Devices (2 bytes)
            ushort numberOfWords = (ushort)values.Length;
            command[8] = (byte)(numberOfWords & 0xFF);
            command[9] = (byte)((numberOfWords >> 8) & 0xFF);

            // Write Data (N bytes, Little Endian)
            for (int i = 0; i < values.Length; i++)
            {
                command[10 + i * 2] = (byte)(values[i] & 0xFF);
                command[11 + i * 2] = (byte)((values[i] >> 8) & 0xFF);
            }

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
                Debug.WriteLine($"[McProtocolService] WriteWordsAsync error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스 값을 읽습니다. (단일 워드)
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <returns>읽은 워드 값.</returns>
        public async Task<ushort> ReadWordAsync(string deviceCode, int address)
        {
            // ReadWordsAsync를 사용하여 단일 워드를 읽습니다.
            ushort[] result = await ReadWordsAsync(deviceCode, address, 1).ConfigureAwait(false);
            if (result != null && result.Length > 0)
            {
                return result[0];
            }
            throw new Exception("MC Protocol 워드 읽기 응답 데이터 없음.");
        }

        /// <summary>
        /// PLC의 워드 디바이스 값을 씁니다. (단일 워드)
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">디바이스 주소.</param>
        /// <param name="value">쓸 워드 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteWordAsync(string deviceCode, int address, ushort value)
        {
            // WriteWordsAsync를 사용하여 단일 워드를 씁니다.
            return await WriteWordsAsync(deviceCode, address, new ushort[] { value }).ConfigureAwait(false);
        }

        /// <summary>
        /// PLC의 워드 디바이스에서 20바이트(10워드) 문자열을 읽습니다.
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <returns>읽은 문자열.</returns>
        public async Task<string> ReadStringDataAsync(string deviceCode, int address)
        {
            try
            {
                await ConnectAsync().ConfigureAwait(false); // 매 호출마다 연결
                const ushort STRING_WORD_LENGTH = 10; // 20 bytes = 10 words
                ushort[] words = await ReadWordsAsync(deviceCode, address, STRING_WORD_LENGTH).ConfigureAwait(false);

                byte[] bytes = new byte[STRING_WORD_LENGTH * 2];
                for (int i = 0; i < STRING_WORD_LENGTH; i++)
                {
                    bytes[i * 2] = (byte)(words[i] & 0xFF);
                    bytes[i * 2 + 1] = (byte)((words[i] >> 8) & 0xFF);
                }
                // ASCII 인코딩을 사용하여 바이트 배열을 문자열로 변환합니다.
                return Encoding.ASCII.GetString(bytes).TrimEnd('\0'); // 널 문자 제거
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] ReadStringDataAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                Disconnect(); // 매 호출마다 연결 해제
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스에 20바이트(10워드) 문자열을 씁니다.
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <param name="value">쓸 문자열 (20바이트로 패딩/잘림).</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteStringDataAsync(string deviceCode, int address, string value)
        {
            try
            {
                await ConnectAsync().ConfigureAwait(false); // 매 호출마다 연결
                const ushort STRING_BYTE_LENGTH = 20;
                const ushort STRING_WORD_LENGTH = 10; // 20 bytes = 10 words

                byte[] stringBytes = Encoding.ASCII.GetBytes(value);
                byte[] paddedBytes = new byte[STRING_BYTE_LENGTH];
                Array.Copy(stringBytes, paddedBytes, Math.Min(stringBytes.Length, STRING_BYTE_LENGTH));

                ushort[] words = new ushort[STRING_WORD_LENGTH];
                for (int i = 0; i < STRING_WORD_LENGTH; i++)
                {
                    words[i] = (ushort)(paddedBytes[i * 2] | (paddedBytes[i * 2 + 1] << 8));
                }
                return await WriteWordsAsync(deviceCode, address, words).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] WriteStringDataAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                Disconnect(); // 매 호출마다 연결 해제
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스에서 32비트 정수(int)를 읽습니다. (2워드)
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <returns>읽은 정수 값.</returns>
        public async Task<int> ReadIntDataAsync(string deviceCode, int address)
        {
            try
            {
                await ConnectAsync().ConfigureAwait(false); // 매 호출마다 연결
                const ushort INT_WORD_LENGTH = 2; // 32-bit int = 2 words
                ushort[] words = await ReadWordsAsync(deviceCode, address, INT_WORD_LENGTH).ConfigureAwait(false);

                // Little Endian으로 ushort 2개를 int로 결합
                int result = (words[0] | (words[1] << 16));
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] ReadIntDataAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                Disconnect(); // 매 호출마다 연결 해제
            }
        }

        /// <summary>
        /// PLC의 워드 디바이스에 32비트 정수(int)를 씁니다. (2워드)
        /// 연결 및 해제를 포함한 단일 시퀀스로 처리됩니다.
        /// </summary>
        /// <param name="deviceCode">디바이스 코드 (예: "D", "R").</param>
        /// <param name="address">시작 디바이스 주소.</param>
        /// <param name="value">쓸 정수 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteIntDataAsync(string deviceCode, int address, int value)
        {
            try
            {
                await ConnectAsync().ConfigureAwait(false); // 매 호출마다 연결
                ushort[] words = new ushort[2];
                words[0] = (ushort)(value & 0xFFFF); // 하위 16비트
                words[1] = (ushort)((value >> 16) & 0xFFFF); // 상위 16비트
                return await WriteWordsAsync(deviceCode, address, words).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McProtocolService] WriteIntDataAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                Disconnect(); // 매 호출마다 연결 해제
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
