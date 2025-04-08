using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class UploadMultipleFilesResult
    {
        public int SuccessCount => SuccessfulUploads.Count;
        public int FailureCount => FailedUploads.Count;
        public List<SuccessfulUploadInfo> SuccessfulUploads { get; set; } = new List<SuccessfulUploadInfo>();
        public List<FailedUploadInfo> FailedUploads { get; set; } = new List<FailedUploadInfo>();
    }

    public class SuccessfulUploadInfo
    {
        public string OriginalFileName { get; set; }
        public string Url { get; set; }
    }

    public class FailedUploadInfo
    {
        public string OriginalFileName { get; set; }
        public string ErrorMessage { get; set; }
    }
}
