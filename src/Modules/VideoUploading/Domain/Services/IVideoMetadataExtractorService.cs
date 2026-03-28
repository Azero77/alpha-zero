using AlphaZero.Modules.VideoUploading.Domain.Models;
using ErrorOr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlphaZero.Modules.VideoUploading.Domain.Services
{
    public interface IVideoSpecificationExtractorService
    {
        Task<ErrorOr<VideoSpecifications>> ExtractAsync(Video video,CancellationToken token = default);
    }
}
