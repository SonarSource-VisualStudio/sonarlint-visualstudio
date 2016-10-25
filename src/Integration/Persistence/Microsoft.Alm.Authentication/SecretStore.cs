﻿using System;
using System.Diagnostics;

namespace Microsoft.Alm.Authentication
{
    /// <summary>
    /// Interface to secure secrets storage which indexes values by target and utilizes the
    /// operating system keychain / secrets vault.
    /// </summary>
    public sealed class SecretStore : BaseSecureStore, ICredentialStore, ITokenStore
    {
        /// <summary>
        /// Creates a new <see cref="SecretStore"/> backed by the operating system keychain /
        /// secrets vault.
        /// </summary>
        /// <param name="storeNamespace">The namespace of the secrets written and read by this store.</param>
        /// <param name="credentialCache">
        /// (optional) Write-through, read-first cache. Default cache is used if a custom cache is
        /// not provided.
        /// </param>
        /// <param name="tokenCache">
        /// (optional) Write-through, read-first cache. Default cache is used if a custom cache is
        /// not provided.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="storeNamespace"/> has <see cref="BaseSecureStore.IllegalCharacters"/></exception>
        public SecretStore(string storeNamespace, ITokenStore tokenCache = null, Secret.UriNameConversion getTargetName = null)
        {
            if (String.IsNullOrWhiteSpace(storeNamespace))
            {
                throw new ArgumentNullException(nameof(storeNamespace));
            }
            if (storeNamespace.IndexOfAny(IllegalCharacters) != -1)
            {
                throw new ArgumentOutOfRangeException(nameof(storeNamespace));
            }

            _getTargetName = getTargetName ?? Secret.UriToName;

            _namespace = storeNamespace;
            _tokenCache = tokenCache ?? new SecretCache(storeNamespace, _getTargetName);
        }

        private string _namespace;
        private ITokenStore _tokenCache;

        private readonly Secret.UriNameConversion _getTargetName;

        /// <summary>
        /// Deletes credentials for target URI from the credential store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which credentials are being deleted</param>
        public void DeleteCredentials(Uri targetUri)
        {
            ValidateTargetUri(targetUri);

            Trace.WriteLine("CredentialStore::DeleteCredentials");

            string targetName = this.GetTargetName(targetUri);

            this.Delete(targetName);
        }

        /// <summary>
        /// Deletes the token for target URI from the token store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which the token is being deleted</param>
        public void DeleteToken(Uri targetUri)
        {
            ValidateTargetUri(targetUri);

            Trace.WriteLine("TokenStore::ReadToken");

            string targetName = this.GetTargetName(targetUri);

            this.Delete(targetName);
            _tokenCache.DeleteToken(targetUri);
        }

        /// <summary>
        /// Reads credentials for a target URI from the credential store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which credentials are being read</param>
        /// <param name="credentials">The credentials from the store; <see langword="null"/> if failure</param>
        /// <returns><see langword="true"/> if success; <see langword="false"/> if failure</returns>
        public bool ReadCredentials(Uri targetUri, out Credential credentials)
        {
            ValidateTargetUri(targetUri);

            string targetName = this.GetTargetName(targetUri);

            Trace.WriteLine("CredentialStore::ReadCredentials");

            credentials = this.ReadCredentials(targetName);

            return credentials != null;
        }

        /// <summary>
        /// Reads a token for a target URI from the token store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which a token is being read</param>
        /// <param name="token">The token from the store; <see langword="null"/> if failure</param>
        /// <returns><see langword="true"/> if success; <see langword="false"/> if failure</returns>
        public bool ReadToken(Uri targetUri, out Token token)
        {
            ValidateTargetUri(targetUri);

            Trace.WriteLine("TokenStore::ReadToken");

            token = null;

            if (!_tokenCache.ReadToken(targetUri, out token))
            {
                string targetName = this.GetTargetName(targetUri);
                token = ReadToken(targetName);
            }

            return token != null;
        }

        /// <summary>
        /// Writes credentials for a target URI to the credential store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which credentials are being stored</param>
        /// <param name="credentials">The credentials to be stored</param>
        public void WriteCredentials(Uri targetUri, Credential credentials)
        {
            ValidateTargetUri(targetUri);
            Credential.Validate(credentials);

            Trace.WriteLine("CredentialStore::WriteCredentials");

            string targetName = this.GetTargetName(targetUri);

            this.WriteCredential(targetName, credentials);
        }

        /// <summary>
        /// Writes a token for a target URI to the token store
        /// </summary>
        /// <param name="targetUri">The URI of the target for which a token is being stored</param>
        /// <param name="token">The token to be stored</param>
        public void WriteToken(Uri targetUri, Token token)
        {
            ValidateTargetUri(targetUri);
            Token.Validate(token);

            Trace.WriteLine("TokenStore::ReadToken");

            string targetName = this.GetTargetName(targetUri);

            _tokenCache.WriteToken(targetUri, token);

            this.WriteToken(targetName, token);
        }

        /// <summary>
        /// Formats a TargetName string based on the TargetUri base on the format started by git-credential-winstore
        /// </summary>
        /// <param name="targetUri">Uri of the target</param>
        /// <returns>Properly formatted TargetName string</returns>
        protected override string GetTargetName(Uri targetUri)
        {
            Debug.Assert(targetUri != null, "The targetUri parameter is null");

            Trace.WriteLine("SecretStore::GetTargetName");

            return _getTargetName(targetUri, _namespace);
        }
    }
}

