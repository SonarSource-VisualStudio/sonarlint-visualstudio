﻿using System;

namespace Microsoft.Alm.Authentication
{
    public interface IAuthentication
    {
        void DeleteCredentials(Uri targetUri);
        bool GetCredentials(Uri targetUri, out Credential credentials);
        bool SetCredentials(Uri targetUri, Credential credentials);
    }
}

