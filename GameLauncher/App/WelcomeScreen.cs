﻿using GameLauncher.App.Classes;
using GameLauncherReborn;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameLauncher.App {
    public partial class WelcomeScreen : Form {
        private readonly IniFile _settingFile = new IniFile("Settings.ini");
        List<CDNObject> CDNList = new List<CDNObject>();
        private bool CDNStatusCheck = false;
        private bool ServerStatusCheck = false;

        public WelcomeScreen() {
            InitializeComponent();
        }

        //Check Serverlist API Status Upon Main Screen load - DavidCarbon
        private async void PingServerListStatus()
        {
            HttpWebRequest requestMainServerListAPI = (HttpWebRequest)HttpWebRequest.Create(Self.mainserver + "/serverlist.json");
            requestMainServerListAPI.AllowAutoRedirect = false;
            requestMainServerListAPI.Method = "HEAD";
            requestMainServerListAPI.UserAgent = "GameLauncher (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)";
            try
            {
                HttpWebResponse mainServerListResponseAPI = (HttpWebResponse)requestMainServerListAPI.GetResponse();
                mainServerListResponseAPI.Close();
                ServerStatusText.Text = "Main Server List - Online";
                ServerStatusCheck = true;
            }
            catch (WebException)
            {
                ServerStatusText.Text = "Main Server List - Offline";

                await Task.Delay(1500);
                ServerStatusText.Text = "Checking Backup API - Pinging";

                await Task.Delay(1500);
                try
                {
                    //Check Using Backup API
                    HttpWebRequest requestBkupServerListAPI = (HttpWebRequest)HttpWebRequest.Create(Self.staticapiserver + "/serverlist.json");
                    requestBkupServerListAPI.AllowAutoRedirect = false;
                    requestBkupServerListAPI.Method = "HEAD";
                    requestBkupServerListAPI.UserAgent = "GameLauncher (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)";
                    try
                    {
                        HttpWebResponse bkupServerListResponseAPI = (HttpWebResponse)requestBkupServerListAPI.GetResponse();
                        bkupServerListResponseAPI.Close();
                        ServerStatusText.Text = "Backup Server List - Online";
                        ServerStatusCheck = true;
                    }
                    catch (WebException)
                    {
                        ServerStatusText.Text = "Server Lists Connection - Error";
                    }
                }
                catch { }
            }
        }

        private async void PingCDNListStatus()
        {
            HttpWebRequest requestMainServerListAPI = (HttpWebRequest)HttpWebRequest.Create(Self.mainserver + "/cdn_list.json");
            requestMainServerListAPI.AllowAutoRedirect = false;
            requestMainServerListAPI.Method = "HEAD";
            requestMainServerListAPI.UserAgent = "GameLauncher (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)";
            try
            {
                HttpWebResponse mainServerListResponseAPI = (HttpWebResponse)requestMainServerListAPI.GetResponse();
                mainServerListResponseAPI.Close();
                CDNStatusText.Text = "Main CDN List - Online";
                CDNStatusCheck = true;
            }
            catch (WebException)
            {
                CDNStatusText.Text = "Main CDN List - Offline";

                await Task.Delay(1500);
                CDNStatusText.Text = "Checking Backup API - Pinging";

                await Task.Delay(1500);
                try
                {
                    //Check Using Backup API
                    HttpWebRequest requestBkupServerListAPI = (HttpWebRequest)HttpWebRequest.Create(Self.staticapiserver + "/cdn_lists.json");
                    requestBkupServerListAPI.AllowAutoRedirect = false;
                    requestBkupServerListAPI.Method = "HEAD";
                    requestBkupServerListAPI.UserAgent = "GameLauncher (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)";
                    try
                    {
                        HttpWebResponse bkupServerListResponseAPI = (HttpWebResponse)requestBkupServerListAPI.GetResponse();
                        bkupServerListResponseAPI.Close();
                        CDNStatusText.Text = "Backup CDN List - Online";
                        CDNStatusCheck = true;
                    }
                    catch (WebException)
                    {
                        CDNStatusText.Text = "CDN Lists Connection - Error";
                    }
                }
                catch { }
            }
        }

        private void WelcomeScreen_Load(object sender, EventArgs e) {
            SettingsFormElements(false);
            PingServerListStatus();
            PingCDNListStatus();

            if (ServerStatusCheck == true || CDNStatusCheck == true)
            {
                Task.Delay(2000);
                SettingsFormElements(true);
            }
        }

        private void SettingsFormElements(bool hideElements = true)
        {
            ShowCDNSources();
            downloadSourceText.Visible = hideElements;
            CDNSource.Visible = hideElements;
            Save.Visible = hideElements;
        }

        private void ShowCDNSources()
        {
            //CDN
            String _slresponse = String.Empty;
            try
            {
                WebClientWithTimeout wc = new WebClientWithTimeout();
                try
                {
                    _slresponse = wc.DownloadString(Self.CDNUrlList);
                }
                catch
                {
                    _slresponse = wc.DownloadString(Self.CDNUrlStaticList);
                }
            }
            catch
            {
                _slresponse = JsonConvert.SerializeObject(new[] {
                    new CDNObject {
                        name = "[CF] WorldUnited.gg Mirror",
                        url = "http://cdn.worldunited.gg/gamefiles/packed/"
                    },
                    new CDNObject {
                        name = "[CF] DavidCarbon Mirror",
                        url = "http://g-sbrw.davidcarbon.download/"
                    }
                });
            }

            CDNList = JsonConvert.DeserializeObject<List<CDNObject>>(_slresponse);

            CDNSource.DisplayMember = "name";
            CDNSource.DataSource = CDNList;
        }

        private void Save_Click(object sender, EventArgs e) {
            CDN.TrackHigh = "1"; //Max Graphics = 1 , Min Graphics = 0
            CDN.CDNUrl = ((CDNObject)CDNSource.SelectedItem).url;

            QuitWithoutSaving_Click(sender, e);
        }

        private void QuitWithoutSaving_Click(object sender, EventArgs e) {
            this.Close();
        }
    }
}
