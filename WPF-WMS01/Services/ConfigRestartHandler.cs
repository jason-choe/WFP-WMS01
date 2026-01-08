using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Xml;
using System.Configuration;

namespace WPF_WMS01.Services
{
    /// <summary>
    /// app.config의 기존 주석과 포맷을 유지하면서 특정 값만 업데이트하고 앱을 재시작합니다.
    /// </summary>
    public class ConfigRestartHandler
    {
        public void SaveAndRestart(Dictionary<string, string> newSettings)
        {
            try
            {
                // 1. 실행 파일의 .config 파일 경로 확인
                // CS1061 오류 해결을 위해 ConfigurationManager를 사용하여 경로를 정확히 가져옵니다.
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string configPath = config.FilePath;

                if (!File.Exists(configPath))
                {
                    MessageBox.Show("설정 파일을 찾을 수 없습니다.");
                    return;
                }

                // 2. XML 문서로 로드 (주석과 화이트스페이스 보존)
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true; // 기존 포맷 유지를 위한 핵심 설정
                doc.Load(configPath);

                // 3. appSettings 섹션 찾기
                XmlNode appSettingsNode = doc.SelectSingleNode("//appSettings");
                if (appSettingsNode != null)
                {
                    foreach (var setting in newSettings)
                    {
                        // 해당 키를 가진 add 노드 찾기
                        XmlElement element = (XmlElement)appSettingsNode.SelectSingleNode($"add[@key='{setting.Key}']");
                        
                        if (element != null)
                        {
                            // 기존 노드가 있으면 value 속성만 업데이트 (주석 영향 없음)
                            element.SetAttribute("value", setting.Value);
                        }
                        else
                        {
                            // 노드가 없으면 새로 생성
                            XmlElement newElem = doc.CreateElement("add");
                            newElem.SetAttribute("key", setting.Key);
                            newElem.SetAttribute("value", setting.Value);
                            appSettingsNode.AppendChild(newElem);
                        }
                    }
                }

                // 4. 파일 저장 (XmlWriterSettings를 통해 원본 포맷 최대한 유지)
                doc.Save(configPath);

                // 5. 프로세스 재시작 실행
                RestartApplicationWithDelay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 업데이트 중 오류 발생: {ex.Message}");
            }
        }

        private void RestartApplication()
        {
            try
            {
                // 하나의 실행파일 만 허용하므로 새로운 restart는 없도록 한다.
                //string appPath = Process.GetCurrentProcess().MainModule.FileName;
                //Process.Start(appPath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재시작 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 중복 실행 방지 로직이 있는 경우, 현재 프로세스가 종료된 후 새 프로세스가 뜨도록 지연 실행합니다.
        /// </summary>
        private void RestartApplicationWithDelay()
        {
            try
            {
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                // cmd.exe를 사용하여 1초 대기 후 앱을 다시 실행하는 명령줄 구성
                // choice 명령어를 사용하여 대기 시간을 주고, 그 뒤에 앱을 실행합니다.
                string command = $"/c timeout /t 5 /nobreak && start \"\" \"{appPath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = appDir
                };

                Process.Start(startInfo);

                // 현재 인스턴스를 즉시 종료하여 뮤텍스를 해제합니다.
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재시작 스크립트 실행 실패: {ex.Message}");
            }
        }
    }
}