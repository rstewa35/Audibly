//   Author: Ryan Stewart
//   Date: 05/20/2022

using System;
using System.Globalization;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using Audibly.Extensions;
using Audibly.Model;
using FlyleafLib.MediaFramework.MediaDemuxer;
using Microsoft.UI.Media.Core;
using Microsoft.UI.Media.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Microsoft.UI.Xaml.Data;
using Audibly.Helpers;
using Audibly.Pages;
using Windows.UI.WindowManagement;

namespace Audibly;

public sealed partial class MainWindow : Window
{
    private readonly ApplicationDataContainer _localSettings;
    private string _curPosStg;
    private Microsoft.UI.Windowing.AppWindow _mainWindow;

    public MainWindow()
    {
        InitializeComponent();
        this.SetWindowSize(315, 440, false, false, true, false);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // ViewModel = new AudiobookViewModel();

        _localSettings = ApplicationData.Current.LocalSettings;
#if DEBUG
        // _localSettings.Values.Clear();
#endif

        CoverImage_Grid.PointerEntered += CoverImage_Grid_PointerEntered;
        CoverImage_Grid.PointerExited += CoverImage_Grid_PointerExited;

        MediaButtons_Control.SkipBack10Button.Click += SkipBack10Button_Click;
        MediaButtons_Control.PlayPauseButton.Click += PlayPauseButton_Click;
        MediaButtons_Control.SkipForward30Button.Click += SkipForward30Button_Click;

        MediaPlayerElementContainer!.Child = MediaPlayerElement;
        MediaPlayer.AutoPlay = false;
        MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
        MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
        MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

        // play/pause button disabled until an audio file is successfully opened
        ToggleAudioControls(false);

        Progress_Control.CurrentTime_TextBlock.Opacity = 0.5;
        Progress_Control.CurrentChapterDuration_TextBlock.Opacity = 0.5;
        if (_localSettings.Values["currentAudiobookPath"] != null)
        {
            var currentAudiobookPath = _localSettings.Values["currentAudiobookPath"].ToString();
            AudiobookViewModel.Audiobook.Init(currentAudiobookPath);

            var file = StorageFile.GetFileFromPathAsync(currentAudiobookPath).GetAwaiter().GetResult(); // gross
            MediaPlayerElement_Init(file);
        }
    }

    private void CoverImage_Grid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (BlackOverlay_Canvas.Visibility == Visibility.Visible) BlackOverlay_Canvas.Visibility = Visibility.Collapsed;
    }

    private void CoverImage_Grid_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (BlackOverlay_Canvas.Visibility == Visibility.Collapsed) BlackOverlay_Canvas.Visibility = Visibility.Visible;
    }

    private TimeSpan CurPos
    {
        get => MediaPlayer.PlaybackSession.Position;
        set => MediaPlayer.PlaybackSession.Position = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private MediaPlayer MediaPlayer
    {
        get
        {
            if (MediaPlayerElement.MediaPlayer == null) MediaPlayerElement.SetMediaPlayer(new MediaPlayer());
            return MediaPlayerElement.MediaPlayer;
        }
    }

    public MediaPlayerElement MediaPlayerElement { get; } = new() { AreTransportControlsEnabled = false };

    private void CompactViewButton_Click(object sender, RoutedEventArgs e)
    {


        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        _mainWindow = appWindow;
        _mainWindow.Hide();

        var newWindow = WindowHelper.CreateWindow();
        var compactPage = new CompactPage();
        newWindow.Content = compactPage;
        newWindow.Activate();
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        switch (sender.PlaybackState)
        {
            case MediaPlaybackState.Playing:
                DispatcherQueue.TryEnqueue(() =>
                {
                    if ((string)MediaButtons_Control.PlayPauseButton.Tag != "pause")
                    {
                        MediaButtons_Control.PlayPauseButton.Tag = "pause";
                        MediaButtons_Control.PlayPauseIcon.Symbol = Symbol.Pause;
                    }
                });
                break;
            case MediaPlaybackState.Paused:
                DispatcherQueue.TryEnqueue(() =>
                {
                    if ((string)MediaButtons_Control.PlayPauseButton.Tag != "play")
                    {
                        MediaButtons_Control.PlayPauseButton.Tag = "play";
                        MediaButtons_Control.PlayPauseIcon.Symbol = Symbol.Play;
                    }
                });
                break;
        }
    }

    private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AudiobookViewModel.Audiobook.Update(MediaPlayer.PlaybackSession.Position.TotalMilliseconds);
            ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(AudiobookViewModel.Audiobook.CurChptr);
            SaveProgress();
        });
    }

    private async void OpenAudiobook_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
#if !WINDOWS_UWP
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
#endif
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".m4b");

        var file = await picker.PickSingleFileAsync()!;
        if (file is null) return;

        _localSettings.Values["currentAudiobookPath"] = file.Path;
        AudiobookViewModel.Audiobook.Init(file.Path);

        MediaPlayerElement_Init(file);
    }

    private void MediaPlayerElement_Init(StorageFile file)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            MediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
            ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(AudiobookViewModel.Audiobook.CurChptr);
            ToggleAudioControls(true);
        });
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var curBookName = Path.GetFileNameWithoutExtension(AudiobookViewModel.Audiobook.FilePath);
            _curPosStg = _curPosStg != curBookName ? curBookName : _curPosStg;

            if (_localSettings.Values[_curPosStg!] == null)
            {
                _localSettings.Values[_curPosStg] = 0;
                MediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(0);
            }
            else
            {
                MediaPlayer.PlaybackSession.Position =
                    TimeSpan.FromMilliseconds(Convert.ToDouble(_localSettings.Values[_curPosStg!]));
            }
        });
    }

    private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        // todo -> add infobar error message
        ToggleAudioControls(false);
    }

    private void ToggleAudioControls(bool isEnabled)
    {
        DispatcherQueue.TryEnqueue(() =>
        { 
            MediaButtons_Control.PlayPauseButton.IsEnabled = isEnabled;
            PreviousChapterButton.IsEnabled = isEnabled;
            MediaButtons_Control.SkipBack10Button.IsEnabled = isEnabled;
            MediaButtons_Control.SkipForward30Button.IsEnabled = isEnabled;
            NextChapterButton.IsEnabled = isEnabled;
            ChapterCombo.IsEnabled = isEnabled;
            AudioLevel_Slider.IsEnabled = isEnabled;

            Progress_Control.CurrentTime_TextBlock.Opacity = Progress_Control.ChapterProgress_ProgressBar.Opacity =
                Progress_Control.CurrentChapterDuration_TextBlock.Opacity = isEnabled ? 1.0 : 0.5;
        });
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if ((string)MediaButtons_Control.PlayPauseButton.Tag == "play")
                MediaPlayer.Play();
            else
                MediaPlayer.Pause();
        });
    }

    private void NextChapterButton_Click(object sender, RoutedEventArgs e)
    {
        CurPos = AudiobookViewModel.Audiobook.GetNextChapter();
        ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(AudiobookViewModel.Audiobook.CurChptr);
    }

    private void PreviousChapterButton_Click(object sender, RoutedEventArgs e)
    {
        CurPos = AudiobookViewModel.Audiobook.GetPrevChapter(CurPos.TotalMilliseconds);
        ChapterCombo.SelectedIndex = ChapterCombo.Items.IndexOf(AudiobookViewModel.Audiobook.CurChptr);
    }

    private void SkipForward30Button_Click(object sender, RoutedEventArgs e)
    {
        CurPos += TimeSpan.FromSeconds(30);
    }

    private void SkipBack10Button_Click(object sender, RoutedEventArgs e)
    {
        CurPos -= TimeSpan.FromSeconds(10);
    }

    private void SaveProgress()
    {
        _localSettings.Values[_curPosStg] =
            MediaPlayer.PlaybackSession.Position.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
    }

    private void ChapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var container = sender as ComboBox;
        if (container == null || container.SelectedItem is not Demuxer.Chapter chapter) return;

        if (ChapterCombo.SelectedIndex == ChapterCombo.Items.IndexOf(AudiobookViewModel.Audiobook.CurChptr)) return;

        CurPos = TimeSpan.FromMilliseconds(chapter.StartTime);
    }

    private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var volume = AudioLevel_Slider.Value;

        DispatcherQueue.TryEnqueue(() =>
        {
            AudiobookViewModel.Audiobook.AudioLevelGlyph = volume == 0 ? Audiobook.Volume0 : volume <= 33 ? Audiobook.Volume1 : volume <= 66 ? Audiobook.Volume2 : Audiobook.Volume3;
            MediaPlayer.Volume = volume / 100;
        });
    }
}