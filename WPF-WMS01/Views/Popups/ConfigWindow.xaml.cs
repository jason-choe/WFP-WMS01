using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using WPF_WMS01.Services;

namespace WPF_WMS01
{
    /// <summary>
    /// 설정 관리 창을 위한 비하인드 코드입니다.
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            // CoreWebView2 환경 초기화 (비동기)
            await myWebView.EnsureCoreWebView2Async();

            // 메시지 수신 이벤트 연결
            myWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // 로컬 HTML 파일 로드
            string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_editor.html");
            myWebView.CoreWebView2.Navigate($"file:///{htmlPath.Replace('\\', '/')}");
        }

        /// <summary>
        /// WebView2(HTML/JS)로부터 메시지를 수신했을 때 호출됩니다.
        /// </summary>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // 전달된 JSON 메시지 읽기
            string json = e.WebMessageAsJson;
            dynamic msg = JsonConvert.DeserializeObject(json);

            string type = msg.type;

            if (type == "GET_INITIAL_CONFIG")
            {
                // HTML 로드 완료 시 현재 app.config의 설정값들을 읽어서 HTML로 전송합니다.
                SendCurrentConfigToWeb();
            }
            else if (type == "SAVE_AND_RESTART")
            {
                // HTML에서 보낸 데이터를 받아 저장하고 앱을 재시작합니다.
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(msg.data.ToString());
                ConfigRestartHandler handler = new ConfigRestartHandler();
                handler.SaveAndRestart(data);
            }
        }

        /// <summary>
        /// 현재 app.config의 appSettings 섹션을 읽어 WebView2로 전송합니다.
        /// </summary>
        private void SendCurrentConfigToWeb()
        {
            try
            {
                // 모든 AppSettings 키-밸류 쌍을 Dictionary로 추출
                var configData = ConfigurationManager.AppSettings.AllKeys
                    .ToDictionary(k => k, k => ConfigurationManager.AppSettings[k]);

                string jsonData = JsonConvert.SerializeObject(configData);

                // HTML 측의 window.chrome.webview.addEventListener('message', ...)로 데이터 전송
                myWebView.CoreWebView2.PostWebMessageAsJson(jsonData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정을 읽어오는 중 오류 발생: {ex.Message}");
            }
        }
    }
}