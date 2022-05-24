//   Author: Ryan Stewart
//   Date: 05/20/2022

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Storage;
using ATL;
using Audibly.Extensions;
using FlyleafLib.MediaFramework.MediaDemuxer;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Data;

namespace Audibly.Model;

public class Audiobook : BindableBase
{
    public const string Volume0 = "\uE74F";
    public const string Volume1 = "\uE993";
    public const string Volume2 = "\uE994";
    public const string Volume3 = "\uE995";

    private string _audioLevelGlyph;
    public string AudioLevelGlyph 
    { 
        get => _audioLevelGlyph;  
        set => SetProperty(ref _audioLevelGlyph, value);
    }

    private string _author;

    private List<Demuxer.Chapter> _chptrs = new();

    private ImageSource _coverImgSrc = new BitmapImage(new Uri("https://via.placeholder.com/500"));

    private Demuxer.Chapter _curChptr;

    private int _curChptrDur;

    private string _curChptrDurText = "00:00:00";

    private string _curChptrTitle;

    private string _curPosInBook;

    private long _curTimeMs;

    private string _curTimeText = "00:00:00";

    private string _description;

    private string _title;

    private Metadata metadata;
    public long Duration { get; private set; }
    public string FilePath { get; private set; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Author
    {
        get => _author;
        set => SetProperty(ref _author, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public List<Demuxer.Chapter> Chptrs
    {
        get => _chptrs ??= new List<Demuxer.Chapter>();
        set => SetProperty(ref _chptrs, value);
    }

    public Demuxer.Chapter CurChptr
    {
        get => _curChptr;
        set
        {
            _curChptr = value;

            CurChptrTitle = _curChptr.Title;
            CurChptrDur = (int)(_curChptr.EndTime - _curChptr.StartTime);
            // CurChptrDurText = _curChptrDur.ToStr_ms();
            var t = TimeSpan.FromMilliseconds(CurChptrDur);
            CurChptrDurText = $@"{(int)t.TotalHours}:{t:mm}:{t:ss}";
        }
    }

    public string CurChptrTitle
    {
        get => _curChptrTitle;
        set => SetProperty(ref _curChptrTitle, value);
    }

    public int CurChptrDur
    {
        get => _curChptrDur;
        set => SetProperty(ref _curChptrDur, value);
    }

    public string CurChptrDurText
    {
        get => _curChptrDurText;
        set => SetProperty(ref _curChptrDurText, value);
    }

    public int CurTimeMs
    {
        get => (int)_curTimeMs;
        set
        {
            SetProperty(ref _curTimeMs, value);
            CurTimeText = _curTimeMs.ToStr_ms();
        }
    }

    public string CurTimeText
    {
        get => _curTimeText;
        set => SetProperty(ref _curTimeText, value);
    }

    public ImageSource CoverImgSrc
    {
        get => _coverImgSrc;
        set => SetProperty(ref _coverImgSrc, value);
    }

    public string CurPosInBook
    {
        get => $"Current position: {_curPosInBook}";
        set => SetProperty(ref _curPosInBook, value);
    }

    private static StorageFolder StorageFolder => ApplicationData.Current.LocalFolder;

    public void Update(double curMs)
    {
        if (!CurChptr.InRange(curMs))
        {
            var tmp = Chptrs.Find(c => c.InRange(curMs));
            if (tmp != null) CurChptr = tmp;
        }

        CurTimeMs = curMs > CurChptr.StartTime ? (int)(curMs - CurChptr.StartTime) : 0;
        CurPosInBook = curMs.ToStr_ms();
    }

    public long GetChapter(Demuxer.Chapter chptr, long curTimeMs)
    {
        if (chptr == CurChptr) return curTimeMs.ToTicks();
        CurChptr = chptr;
        CurTimeMs = 0;
        CurPosInBook = CurChptr.StartTime.ToStr_ms();
        return CurChptr.StartTime.ToTicks();
    }

    public TimeSpan GetNextChapter()
    {
        var idx = Chptrs.IndexOf(CurChptr);

        // if 'CurChptr' is the last chapter of the book
        if (idx == Chptrs.Count - 1)
        {
            CurChptr = Chptrs[idx];
            CurTimeMs = (int)CurChptr.EndTime;
            CurPosInBook = CurChptr.EndTime.ToStr_ms();
            return TimeSpan.FromMilliseconds(CurChptr.EndTime);
        }

        CurChptr = Chptrs[idx + 1];
        CurTimeMs = 0;
        CurPosInBook = CurChptr.StartTime.ToStr_ms();
        return TimeSpan.FromMilliseconds(CurChptr.StartTime);
    }

    public TimeSpan GetPrevChapter(double curTimeMs)
    {
        var idx = Chptrs.FindIndex(c => c.InRange(curTimeMs));
        if (idx == -1) return TimeSpan.FromMilliseconds(curTimeMs);

        // RETURNS start of the current chapter IF 'curTimeMs' is in the 1st chapter of the book
        //     OR
        // the current position in 'CurChptr' is greater than 3 seconds away from the start of 'CurChptr'
        CurChptr = idx == 0 || (curTimeMs > Chptrs[idx].StartTime && curTimeMs - Chptrs[idx].StartTime > 3000)
            ? Chptrs[idx]
            : Chptrs[idx - 1];

        CurTimeMs = 0;
        CurPosInBook = CurChptr.StartTime.ToStr_ms();
        return TimeSpan.FromMilliseconds(CurChptr.StartTime);
    }

    public void Init(string filePath)
    {
        FilePath = filePath;
        var fileMetadata = new Track(filePath);

        var bookAppdataDir = StorageFolder.CreateFolderAsync(
            $"{Path.GetFileNameWithoutExtension(filePath)} [{fileMetadata.Artist}]",
            CreationCollisionOption.OpenIfExists).GetAwaiter().GetResult();

        StorageFile coverImage;

        if (!File.Exists(Path.Combine(bookAppdataDir.Path, $"{nameof(Metadata)}.json")))
        {
            metadata = new Metadata
            {
                Title = fileMetadata.Title,
                Author = fileMetadata.Artist,
                Description = fileMetadata.Comment,
                Duration = fileMetadata.Duration.ToMs(),
                Chapters = new List<Demuxer.Chapter>()
            };

            foreach (var ch in fileMetadata.Chapters)
            {
                var chptr = new Demuxer.Chapter
                {
                    Title = ch.Title,
                    StartTime = ch.StartTime,
                    EndTime = ch.EndTime
                };
                metadata.Chapters.Add(chptr);
            }
             
            var imageBytes = fileMetadata.EmbeddedPictures.FirstOrDefault()?.PictureData;
            coverImage = bookAppdataDir.CreateFileAsync("CoverImage.jpg", CreationCollisionOption.ReplaceExisting)
                .GetAwaiter().GetResult();
            FileIO.WriteBytesAsync(coverImage, imageBytes).GetAwaiter().GetResult();

            var metadataFile = bookAppdataDir.CreateFileAsync("Metadata.json", CreationCollisionOption.ReplaceExisting)
                .GetAwaiter().GetResult();
            File.WriteAllText(metadataFile.Path,
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            metadata = JsonSerializer.Deserialize<Metadata>(
                File.ReadAllText(Path.Combine(bookAppdataDir.Path, $"{nameof(Metadata)}.json")));
            coverImage = bookAppdataDir.GetFileAsync("CoverImage.jpg").GetAwaiter().GetResult();
        }

        Debug.Assert(metadata != null, nameof(metadata) + " != null");

        Title = metadata.Title;
        Author = metadata.Author;
        Description = metadata.Description;
        Duration = metadata.Duration;
        Chptrs = metadata.Chapters;

        CurChptr = Chptrs[0];
        CurTimeMs = 0;
        CurPosInBook = "0";
        AudioLevelGlyph = Volume3;

        var bitmapImage = new BitmapImage(new Uri(coverImage.Path)) { DecodePixelWidth = 500 };
        CoverImgSrc = bitmapImage;
    }
}

public class AudiobookViewModel
{
    public static Audiobook Audiobook { get; } = new();
}