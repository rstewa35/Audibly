﻿using System;
using System.ComponentModel;
using System.IO;
using Windows.Storage.Pickers;
using Audibly.Model;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Windows.Storage;
using FlyleafLib.MediaFramework.MediaDemuxer;

namespace Audibly;

public sealed partial class MainWindow : Window
{
    private readonly Player _player;
    private bool _lockUpdate;
    private readonly ApplicationDataContainer _localSettings;
    private string _curPosStg = "";

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new AudiobookViewModel();
        _localSettings = ApplicationData.Current.LocalSettings;

        Engine.Start(
            new EngineConfig
            {
                UIRefresh = true,
                FFmpegPath = $"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FFMpeg")}"
            }
        );

        // Create new config
        var config = new Config { Player = { Usage = Usage.Audio } };
        _player = new Player(config);

        // Listen to property changed events
        _player.PropertyChanged += Player_PropertyChanged;
        _player.Audio.PropertyChanged += PlayerAudio_PropertyChanged;

        // Allow auto play on open
        config.Player.AutoPlay = false;

        // Prepare Seek Offsets and Commands
        config.Player.SeekOffset = TimeSpan.FromSeconds(10).Ticks;
        config.Player.SeekOffset2 = TimeSpan.FromSeconds(30).Ticks;

        _player.OpenCompleted += (o, e) =>
        {
            Utils.UI(() =>
            {
                if (_curPosStg == string.Empty) _curPosStg = Path.GetFileNameWithoutExtension(ViewModel.Audiobook.FilePath);

                if (_localSettings.Values[_curPosStg!] == null)
                {
                    _localSettings.Values[_curPosStg] = 0;
                    _player.CurTime = 0;
                }
                else { _player.CurTime = TimeSpan.FromMilliseconds(Convert.ToInt32(_localSettings.Values[_curPosStg!])).Ticks; }
            });
        };

        // play/pause button disabled until an audio file is successfully opened
        ToggleAudioControls(false);
        if (_localSettings.Values["currentAudiobookPath"] != null)
        {
            var currentAudiobookPath = _localSettings.Values["currentAudiobookPath"].ToString();
            ViewModel.Audiobook.Init(currentAudiobookPath);
            _player.OpenAsync(currentAudiobookPath);
        }
    }

    public AudiobookViewModel ViewModel { get; set; }

    private void ToggleAudioControls(bool isEnabled)
    {
        PlayPauseButton.IsEnabled = isEnabled;
        PreviousChapterButton.IsEnabled = isEnabled;
        SkipBack10Button.IsEnabled = isEnabled;
        SkipForward30Button.IsEnabled = isEnabled;
        NextChapterButton.IsEnabled = isEnabled;
        ChapterCombo.IsEnabled = isEnabled;

        CurrentTime_TextBlock.Opacity = ChapterProgress_ProgressBar.Opacity =
            CurrentChapterDuration_TextBlock.Opacity = isEnabled ? 1.0 : 0.5;
    }

    private void PlayerAudio_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "Volume":
                // volumeChangedFromLib = true;
                // lblVolume.Text = Player.Audio.Volume.ToString() + "%";
                // sliderVolume.Value = Player.Audio.Volume;
                // volumeChangedFromLib = false;
                break;

            case "Mute":
                // btnMute.Text = Player.Audio.Mute ? "Unmute" : "Mute";
                break;
        }
    }

    private void Player_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "CurTime":
                if (_lockUpdate) return;
                ViewModel.Audiobook.Update(_player.CurTime.ToMs());
                if (ChapterCombo.SelectedIndex != ChapterCombo.Items.IndexOf(ViewModel.Audiobook.CurChptr))
                {
                    ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(ViewModel.Audiobook.CurChptr);
                }
                SaveProgress();
                break;

            case "Duration": break;

            case "Status": break;

            case "CanPlay":
                ToggleAudioControls(true);
                break;
        }
    }

    private async void OpenAudiobook_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(filePicker, hwnd);
        filePicker.FileTypeFilter.Add(".m4a");
        filePicker.FileTypeFilter.Add(".m4b");
        var file = await filePicker.PickSingleFileAsync();
        if (file == null) return;

        _localSettings.Values["currentAudiobookPath"] = file.Path;
        ViewModel.Audiobook.Init(file.Path);
        _player.OpenAsync(file.Path);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _player.TogglePlayPause();

        if ((string)PlayPauseButton.Tag == "play")
        {
            // PlayPauseButton.Icon = new SymbolIcon(Symbol.Pause);
            // PlayPauseButton.Label = "Pause";
            PlayPauseButton.Tag = "pause";
            PlayPauseIcon.Symbol = Symbol.Pause;
        }
        else
        {
            // PlayPauseButton.Icon = new SymbolIcon(Symbol.Play);
            // PlayPauseButton.Label = "Play";
            PlayPauseButton.Tag = "play";
            PlayPauseIcon.Symbol = Symbol.Play;
        }
    }

    private void NextChapterButton_Click(object sender, RoutedEventArgs e)
    {
        _lockUpdate = true;
        _player.CurTime = ViewModel.Audiobook.GetNextChapter();
        ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(ViewModel.Audiobook.CurChptr);
        _lockUpdate = false;
    }

    private void PreviousChapterButton_Click(object sender, RoutedEventArgs e)
    {
        _lockUpdate = true;
        _player.CurTime = ViewModel.Audiobook.GetPrevChapter(_player.CurTime.ToMs());
        ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(ViewModel.Audiobook.CurChptr);
        _lockUpdate = false;
    }

    private void SkipForward30Button_Click(object sender, RoutedEventArgs e)
    {
        _player.SeekForward2();
    }

    private void SkipBack10Button_Click(object sender, RoutedEventArgs e)
    {
        _player.SeekBackward();
    }

    private void SettingButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void SaveProgress()
    {
        _localSettings.Values[_curPosStg] = _player.CurTime.ToMs();
    }

    private void ChapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var container = sender as ComboBox;
        if (container == null) return;
        var chapter = container.SelectedItem as Demuxer.Chapter;
        if (chapter == null) return;

        _lockUpdate = true;
        _player.CurTime = ViewModel.Audiobook.GetChapter(chapter, _player.CurTime.ToMs());
        _lockUpdate = false;
    }
}