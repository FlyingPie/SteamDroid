﻿using System;

using Android.App;
using Android.Content;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using Android.OS;

using SteamKit2;
using SteamDroid.Api;
using SteamDroid.Util;
using System.Threading;

namespace SteamDroid.App
{
    [Activity(Label = "Steam Droid", MainLauncher = true, Icon = "@drawable/LauncherIcon")]
    public class Main : Activity, ICallbackHandler, IServiceConnection
    {
        private EditText inputAuthKey;
        
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            RequestWindowFeature(WindowFeatures.NoTitle);

            SetContentView(Resource.Layout.Main);

            Button buttonConnect = FindViewById<Button>(Resource.Id.ButtonConnect);
            buttonConnect.Click += ClickConnect;
            
            Button buttonFriends = FindViewById<Button>(Resource.Id.ButtonFriends);
            buttonFriends.Click += ClickFriends;

            SteamAlerts.Initialize(this);

            if (!SteamService.IsRunning())
            {
                Intent steamService = new Intent(this, typeof(SteamService));
                StartService(steamService);
            }

            UpdateButtons();
            
            SteamService.GetClient().AddHandler(this);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.OptionsMenu, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            Steam client = SteamService.GetClient();

            switch (item.ItemId)
            {
                case Resource.Id.button_state_1:
                    client.Friends.SetPersonaState(EPersonaState.Online);
                    break;
                case Resource.Id.button_state_2:
                    client.Friends.SetPersonaState(EPersonaState.Away);
                    break;
                case Resource.Id.button_state_3:
                    client.Friends.SetPersonaState(EPersonaState.Busy);
                    break;
                case Resource.Id.button_state_4:
                    client.Friends.SetPersonaState(EPersonaState.Snooze);
                    break;
                case Resource.Id.button_state_5:
                    client.Friends.SetPersonaState(EPersonaState.Offline);
                    break;
                case Resource.Id.button_disconnect:
                    SteamService.DisableAutoReconnect();
                    client.Disconnect();
                    break;
                case Resource.Id.button_settings:
                    Intent intentSettings = new Intent(this, typeof(Preferences));
                    StartActivity(intentSettings);
                    break;
            }
            return base.OnOptionsItemSelected(item);
        }

        public override bool OnPrepareOptionsMenu(IMenu menu)
        {
            bool connected = (SteamService.GetClient() != null && SteamService.GetClient().LoggedIn);

            menu.GetItem(0).SetEnabled(connected);
            menu.GetItem(1).SetEnabled(connected);

            return base.OnPrepareOptionsMenu(menu);
        }
        
        public void HandleCallback(CallbackMsg msg)
        {
            if(msg.IsType<SteamClient.ConnectCallback>())
            {
                int retryCount = SteamService.GetClient().GetRetryCount();
                String retries = (retryCount > 0) ? " (retry " + retryCount + ")" : "";
                SteamAlerts.ShowProgressDialog("Connecting", "Logging in..." + retries, this);
            }
            else if(msg.IsType<SteamUser.LogOnCallback>())
            {
                SteamUser.LogOnCallback callback = (SteamUser.LogOnCallback)msg;
                
                if(callback.Result == EResult.AccountLogonDenied)
                {
                    RequestAuthKey();
                }
                else if(callback.Result == EResult.InvalidLoginAuthCode)
                {
                    InvalidAuthKey();
                }
                else if(callback.Result == EResult.InvalidPassword)
                {
                    SteamAlerts.ShowAlertDialog("Invalid credentials", "Invalid username or password", this);
                }
                else if(callback.Result == EResult.AlreadyLoggedInElsewhere)
                {
                    SteamAlerts.ShowAlertDialog("Already logged in", "This Steam account is already logged in elsewhere", this);
                }
            }
            else if(msg.IsType<SteamUser.LoggedOffCallback>())
            {
                SteamUser.LoggedOffCallback callback = (SteamUser.LoggedOffCallback)msg;
                
                if(callback.Result == EResult.InvalidProtocolVer)
                {
                    SteamAlerts.ShowAlertDialog("Error", "Invalid protocol version", this);
                }
            }
            
            UpdateButtons();
        }
        
        private void ClickConnect(object sender, EventArgs e)
        {
            Connect();
        }
        
        private void ClickFriends(object sender, EventArgs e)
        {
            Intent intent = new Intent(this, typeof(Friends));
            StartActivity(intent);
        }

        private void ClickSendAuthKey(object sender, DialogClickEventArgs e)
        {
            SteamAlerts.HideInputDialog();
            
            if(inputAuthKey.Text.Length > 0)
            {
                Connect(inputAuthKey.Text);
            }
            else
            {
                RequestAuthKey();
                SteamAlerts.ShowAlertDialog("Invalid key", "Please enter a valid auth key", this);
            }
            
            SteamAlerts.EnableAlerts();
        }
        
        private void Connect()
        {
            Connect(null);
        }
        
        private void Connect(String authCode)
        {
            ISharedPreferences pref = PreferenceManager.GetDefaultSharedPreferences(this);
            
            String username = pref.GetString("prefUsername", "");
            String password = pref.GetString("prefPassword", "");
            
            if(username.Length > 0 && password.Length > 0)
            {
                SteamService.GetClient().Connect(username, password, authCode);
                SteamAlerts.ShowProgressDialog("Connecting", "Connecting to the Steam servers...", this);
            }
            else
            {
                SteamAlerts.ShowAlertDialog("Warning", "No valid username or password entered", this);
            }
        }
        
        private void RequestAuthKey()
        {
            RequestAuthKey("Please fill in the auth key that has been send to your email address");
        }
        
        private void RequestAuthKey(String message)
        {
            SteamAlerts.DisableAlerts();

            inputAuthKey = new EditText(this);

            SteamAlerts.ShowInputDialog("Auth key", message, inputAuthKey, ClickSendAuthKey, this);
        }
        
        private void InvalidAuthKey()
        {
            RequestAuthKey("The given auth key is invalid or expired, please try again");
        }
        
        private void UpdateButtons()
        {
            bool connected = SteamService.GetClient() != null && SteamService.GetClient().LoggedIn;
            
            Button buttonConnect = FindViewById<Button>(Resource.Id.ButtonConnect);
            buttonConnect.Enabled = !connected;
            
            Button buttonFriends = FindViewById<Button>(Resource.Id.ButtonFriends);
            buttonFriends.Enabled = connected;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            Console.WriteLine("Connected to SteamService");
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            Console.WriteLine("Disconnected from SteamService");
        }
    }
}

