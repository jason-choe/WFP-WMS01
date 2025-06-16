// Services/HttpService.cs
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WPF_WMS01.Models;

namespace WPF_WMS01.Services
{
    public class HttpService
    {
        private readonly HttpClient _httpClient;
        private string _authToken;

        public string BaseApiUrl { get; }

        // Newtonsoft.Json 직렬화/역직렬화를 위한 설정 객체를 static으로 선언하여 재사용
        private static readonly JsonSerializerSettings _jsonSettings;
        static HttpService() // static 생성자에서 설정 객체 초기화
        {
            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(), // <--- PascalCase를 camelCase로 변환
                // Null 값을 직렬화에 포함할지 여부 등 다른 설정도 필요하면 여기에 추가
                NullValueHandling = NullValueHandling.Ignore // 예시: null 값은 JSON에 포함하지 않음
            };
        }

        public HttpService(string baseApiUrl)
        {
            _httpClient = new HttpClient();
            BaseApiUrl = baseApiUrl;
            _httpClient.BaseAddress = new Uri(baseApiUrl);
        }

        public void SetAuthorizationHeader(string token)
        {
            _authToken = token;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        }

        public void ClearAuthorizationHeader()
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
        }

        // GET 요청
        public async Task<T> GetAsync<T>(string endpoint)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GET Response Body ({endpoint}): {responseBody}"); // 응답 로깅

                // 역직렬화 시에도 _jsonSettings 사용
                return JsonConvert.DeserializeObject<T>(responseBody, _jsonSettings);
            }
            catch (HttpRequestException ex)
            {
                // 네트워크 오류, DNS 문제 등
                Debug.WriteLine($"GET 요청 오류 ({endpoint}): {ex.Message}");
                throw; // 예외를 호출자에게 다시 던짐
            }
            catch (JsonException ex)
            {
                // JSON 역직렬화 오류
                Debug.WriteLine($"JSON 역직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // 기타 예상치 못한 오류
                Debug.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // POST 요청
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                // Newtonsoft.Json.JsonConvert.SerializeObject 사용, 설정 객체 전달
                var jsonContent = JsonConvert.SerializeObject(data, _jsonSettings);

                Console.WriteLine($"Sending POST request to: {BaseApiUrl}{endpoint}");
                Console.WriteLine($"Request Body (JSON): {jsonContent}"); // 이제 camelCase로 출력될 것입니다.

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, httpContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response Body: {responseString}");

                // 역직렬화 시에도 동일한 설정 객체 전달
                return JsonConvert.DeserializeObject<TResponse>(responseString, _jsonSettings);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"POST 요청 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON 직렬화/역직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // POST 요청 (응답 본문이 없을 경우 또는 응답 타입을 특정하지 않는 경우)
        public async Task PostAsync<TRequest>(string endpoint, TRequest data)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(data);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"POST 요청 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // 필요하다면 PUT, DELETE 등의 메서드도 유사하게 추가할 수 있습니다.
        // 새 명세에 맞는 Login 메서드 (옵션: MainViewModel에서 직접 PostAsync 호출해도 됨)
        public async Task<LoginResponse> Login(string username, string password)
        {
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password,
                ApiVersion = new ApiVersion { Major = 0, Minor = 0 } // API 버전 정보 추가
            };
            // 변경된 엔드포인트 사용
            return await PostAsync<LoginRequest, LoginResponse>("wms/rest/login", loginRequest);
        }
    }
}