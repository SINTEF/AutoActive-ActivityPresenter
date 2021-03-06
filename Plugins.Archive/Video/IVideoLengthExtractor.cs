﻿using System;
using System.Threading;
using System.Threading.Tasks;

using SINTEF.AutoActive.FileSystem;

namespace SINTEF.AutoActive.Plugins.ArchivePlugins.Video
{
    public interface IVideoLengthExtractor
    {
        Task<long> GetLengthAsync();
        long ReportedLength { get; }
        void Restart();
    }

    public interface IVideoLengthExtractorFactory
    {
        Task<IVideoLengthExtractor> CreateVideoDecoder(IReadSeekStreamFactory file, string mime, long suggestedLength);
    }
}
