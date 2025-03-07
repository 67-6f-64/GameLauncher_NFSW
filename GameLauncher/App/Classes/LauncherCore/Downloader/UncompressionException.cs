﻿using System;
using System.Runtime.Serialization;

namespace GameLauncher.App.Classes.LauncherCore.Downloader
{
    [Serializable]
    public class UncompressionException : Exception
    {
        public int mErrorCode;

        public int ErrorCode
        {
            get { return this.mErrorCode; }
        }

        public UncompressionException(int errorCode)
        {
            this.mErrorCode = errorCode;
        }

        public UncompressionException(int errorCode, string message) : base(message)
        {
            this.mErrorCode = errorCode;
        }

        public UncompressionException(int errorCode, string message, Exception innerException) : base(message, innerException)
        {
            this.mErrorCode = errorCode;
        }

        protected UncompressionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}