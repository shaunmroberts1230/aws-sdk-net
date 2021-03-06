﻿/*
 * Copyright 2010-2013 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Amazon.Util;
using Amazon.Runtime.Internal.Util;

namespace Amazon.Runtime.Internal.Auth
{
    /// <summary>
    /// AWS4 protocol signer for service calls that transmit authorization in the header field "Authorization".
    /// </summary>
    internal class AWS4Signer : AbstractAWSSigner
    {
        public const string Scheme = "AWS4";
        public const string Algorithm = "HMAC-SHA256";
        public const string Terminator = "aws4_request";
        public static readonly byte[] TerminatorBytes = Encoding.UTF8.GetBytes(Terminator);

        public const string Credential = "Credential";
        public const string SignedHeaders = "SignedHeaders";
        public const string Signature = "Signature";

        public const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        public const string StreamingBodySha256 = "STREAMING-AWS4-HMAC-SHA256-PAYLOAD";

        public const string HostHeader = "host";
        public const string AuthorizationHeader = "Authorization";

        internal const string XAmzDate = "X-Amz-Date";
        internal const string XAmzSignedHeaders = "X-Amz-SignedHeaders";
        internal const string XAmzContentSha256 = "X-Amz-Content-SHA256";
        internal const string XAmzDecodedContentLength = "X-Amz-Decoded-Content-Length";

        static readonly Regex CompressWhitespaceRegex = new Regex("\\s+");
        const SigningAlgorithm SignerAlgorithm = SigningAlgorithm.HmacSHA256;

        public override ClientProtocol Protocol
        {
            get { return ClientProtocol.RestProtocol; }
        }

        /// <summary>
        /// Calculates and signs the specified request using the AWS4 signing protocol by using the
        /// AWS account credentials given in the method parameters. The resulting signature is added
        /// to the request headers as 'Authorization'. Parameters supplied in the request, either in
        /// the resource path as a query string or in the Parameters collection must not have been
        /// uri encoded. If they have, use the SignRequest method to obtain a signature.
        /// </summary>
        /// <param name="request">
        /// The request to compute the signature for. Additional headers mandated by the AWS4 protocol 
        /// ('host' and 'x-amz-date') will be added to the request before signing.
        /// </param>
        /// <param name="clientConfig">
        /// Client configuration data encompassing the service call (notably authentication
        /// region, endpoint and service name).
        /// </param>
        /// <param name="metrics">
        /// Metrics for the request
        /// </param>
        /// <param name="awsAccessKeyId">
        /// The AWS public key for the account making the service call.
        /// </param>
        /// <param name="awsSecretAccessKey">
        /// The AWS secret key for the account making the call, in clear text.
        /// </param>
        /// <exception cref="Amazon.Runtime.SignatureException">
        /// If any problems are encountered while signing the request.
        /// </exception>
        public override void Sign(IRequest request, 
                                  ClientConfig clientConfig, 
                                  RequestMetrics metrics, 
                                  string awsAccessKeyId, 
                                  string awsSecretAccessKey)
        {
            var signingResult = SignRequest(request, clientConfig, metrics, awsAccessKeyId, awsSecretAccessKey);
            request.Headers[AuthorizationHeader] = signingResult.ForAuthorizationHeader;
        }

        /// <summary>
        /// Calculates and signs the specified request using the AWS4 signing protocol by using the
        /// AWS account credentials given in the method parameters.
        /// </summary>
        /// <param name="request">
        /// The request to compute the signature for. Additional headers mandated by the AWS4 protocol 
        /// ('host' and 'x-amz-date') will be added to the request before signing.
        /// </param>
        /// <param name="clientConfig">
        /// Client configuration data encompassing the service call (notably authentication
        /// region, endpoint and service name).
        /// </param>
        /// <param name="metrics">
        /// Metrics for the request.
        /// </param>
        /// <param name="awsAccessKeyId">
        /// The AWS public key for the account making the service call.
        /// </param>
        /// <param name="awsSecretAccessKey">
        /// The AWS secret key for the account making the call, in clear text.
        /// </param>
        /// <exception cref="Amazon.Runtime.SignatureException">
        /// If any problems are encountered while signing the request.
        /// </exception>
        /// <remarks>
        /// Parameters passed as part of the resource path should be uri-encoded prior to
        /// entry to the signer. Parameters passed in the request.Parameters collection should
        /// be not be encoded; encoding will be done for these parameters as part of the 
        /// construction of the canonical request.
        /// </remarks>
        public AWS4SigningResult SignRequest(IRequest request,
                                             ClientConfig clientConfig,
                                             RequestMetrics metrics,
                                             string awsAccessKeyId,
                                             string awsSecretAccessKey)
        {
            var signedAt = InitializeHeaders(request.Headers, request.Endpoint);
            var region = DetermineRegion(clientConfig);
            var service = DetermineService(clientConfig);
            
            var resourcePathParamStart = -1;
            var resourcePath = string.Empty;

            // Extract the true resource path, less any query parameters or sub resource
            if (!string.IsNullOrEmpty(request.ResourcePath))
            {
                resourcePathParamStart = request.ResourcePath.IndexOf('?');
                resourcePath = resourcePathParamStart == -1 ? request.ResourcePath : request.ResourcePath.Substring(0, resourcePathParamStart);
            }

            // if UseQueryString is indicated and Parameters are present, canonicalize those (including uri encoding them)
            // otherwise if we spotted parameters in the resource path, canonicalize those instead (which should be encoded
            // already)
            var canonicalQueryParams = string.Empty;
            if (request.UseQueryString && request.Parameters.Count > 0)
                canonicalQueryParams = CanonicalizeQueryParameters(request.Parameters);
            else if (resourcePathParamStart != -1)
                canonicalQueryParams = CanonicalizeQueryParameters(request.ResourcePath.Substring(resourcePathParamStart + 1), false);

            var bodyHash = SetRequestBodyHash(request);
            var sortedHeaders = SortHeaders(request.Headers);
            var canonicalRequest = CanonicalizeRequest(resourcePath,
                                                       request.HttpMethod,
                                                       sortedHeaders,
                                                       canonicalQueryParams,
                                                       bodyHash);
            if (metrics != null)
                metrics.AddProperty(Metric.CanonicalRequest, canonicalRequest);

            return ComputeSignature(awsAccessKeyId,
                                    awsSecretAccessKey,
                                    region,
                                    signedAt,
                                    service,
                                    CanonicalizeHeaderNames(sortedHeaders),
                                    canonicalRequest,
                                    metrics);
        }

        #region Public Signing Helpers

        /// <summary>
        /// Sets the AWS4 mandated 'host' and 'x-amz-date' headers, returning the date/time that will
        /// be used throughout the signing process in various elements and formats.
        /// </summary>
        /// <param name="headers">The current set of headers</param>
        /// <param name="requestEndpoint"></param>
        /// <returns>Date and time used for x-amz-date, in UTC</returns>
        public static DateTime InitializeHeaders(IDictionary<string, string> headers, Uri requestEndpoint)
        {
            return InitializeHeaders(headers, requestEndpoint, DateTime.UtcNow);
        }

        /// <summary>
        /// Sets the AWS4 mandated 'host' and 'x-amz-date' headers, accepting and returning the date/time that will
        /// be used throughout the signing process in various elements and formats.
        /// </summary>
        /// <param name="headers">The current set of headers</param>
        /// <param name="requestEndpoint"></param>
        /// <param name="requestDateTime"></param>
        /// <returns>Date and time used for x-amz-date, in UTC</returns>
        public static DateTime InitializeHeaders(IDictionary<string, string> headers, Uri requestEndpoint, DateTime requestDateTime)
        {
            // clean up any prior signature in the headers if resigning
            headers.Remove(AuthorizationHeader);

            if (!headers.ContainsKey(HostHeader))
            {
                var hostHeader = requestEndpoint.Host;
                if (!requestEndpoint.IsDefaultPort)
                    hostHeader += ":" + requestEndpoint.Port;
                headers.Add(HostHeader, hostHeader);
            }

            var dt = requestDateTime;
            headers[XAmzDate] = dt.ToString(AWSSDKUtils.ISO8601BasicDateTimeFormat, CultureInfo.InvariantCulture);

            return dt;
        }

        /// <summary>
        /// Computes and returns an AWS4 signature for the specified canonicalized request
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="region"></param>
        /// <param name="signedAt"></param>
        /// <param name="service"></param>
        /// <param name="signedHeaders"></param>
        /// <param name="canonicalRequest"></param>
        /// <returns></returns>
        public static AWS4SigningResult ComputeSignature(ImmutableCredentials credentials,
                                                         string region,
                                                         DateTime signedAt,
                                                         string service,
                                                         string signedHeaders,
                                                         string canonicalRequest)
        {
            return ComputeSignature(credentials.AccessKey,
                                    credentials.SecretKey,
                                    region,
                                    signedAt,
                                    service,
                                    signedHeaders,
                                    canonicalRequest);
        }

        /// <summary>
        /// Computes and returns an AWS4 signature for the specified canonicalized request
        /// </summary>
        /// <param name="awsAccessKey"></param>
        /// <param name="awsSecretAccessKey"></param>
        /// <param name="region"></param>
        /// <param name="signedAt"></param>
        /// <param name="service"></param>
        /// <param name="signedHeaders"></param>
        /// <param name="canonicalRequest"></param>
        /// <param name="metrics"></param>
        /// <returns></returns>
        public static AWS4SigningResult ComputeSignature(string awsAccessKey,
                                                         string awsSecretAccessKey,
                                                         string region,
                                                         DateTime signedAt,
                                                         string service,
                                                         string signedHeaders,
                                                         string canonicalRequest,
                                                         RequestMetrics metrics = null)
        {
            var dateStamp = FormatDateTime(signedAt, AWSSDKUtils.ISO8601BasicDateFormat);
            var scope = string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", dateStamp, region, service, Terminator);

            var stringToSign = new StringBuilder();
            stringToSign.AppendFormat(CultureInfo.InvariantCulture, "{0}-{1}\n{2}\n{3}\n",
                                      Scheme,
                                      Algorithm,
                                      FormatDateTime(signedAt, AWSSDKUtils.ISO8601BasicDateTimeFormat),
                                      scope);

            var canonicalRequestHashBytes = ComputeHash(canonicalRequest);
            stringToSign.Append(AWSSDKUtils.ToHex(canonicalRequestHashBytes, true));

            if (metrics != null)
                metrics.AddProperty(Metric.StringToSign, stringToSign);

            var key = ComposeSigningKey(awsSecretAccessKey,
                                        region,
                                        dateStamp,
                                        service);

            var signature = ComputeKeyedHash(SignerAlgorithm, key, stringToSign.ToString());
            return new AWS4SigningResult(awsAccessKey, signedAt, signedHeaders, scope, key, signature);
        }

        /// <summary>
        /// Formats the supplied date and time for use in AWS4 signing, where various formats are used.
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="formatString">The required format</param>
        /// <returns>The UTC date/time in the requested format</returns>
        public static string FormatDateTime(DateTime dt, string formatString)
        {
            return dt.ToString(formatString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Compute and return the multi-stage signing key for the request.
        /// </summary>
        /// <param name="awsSecretAccessKey">The clear-text AWS secret key, if not held in secureKey</param>
        /// <param name="region">The region in which the service request will be processed</param>
        /// <param name="date">Date of the request, in yyyyMMdd format</param>
        /// <param name="service">The name of the service being called by the request</param>
        /// <returns>Computed signing key</returns>
        public static byte[] ComposeSigningKey(string awsSecretAccessKey, string region, string date, string service)
        {
            char[] ksecret = null;

            try
            {
                ksecret = (Scheme + awsSecretAccessKey).ToCharArray();

                var hashDate = ComputeKeyedHash(SignerAlgorithm, Encoding.UTF8.GetBytes(ksecret), Encoding.UTF8.GetBytes(date));
                var hashRegion = ComputeKeyedHash(SignerAlgorithm, hashDate, Encoding.UTF8.GetBytes(region));
                var hashService = ComputeKeyedHash(SignerAlgorithm, hashRegion, Encoding.UTF8.GetBytes(service));
                return ComputeKeyedHash(SignerAlgorithm, hashService, TerminatorBytes);
            }
            finally
            {
                // clean up all secrets, regardless of how initially seeded (for simplicity)
                if (ksecret != null)
                    Array.Clear(ksecret, 0, ksecret.Length);
            }
        }

        /// <summary>
        /// If the caller has already set the x-amz-content-sha256 header with a pre-computed
        /// content hash, or it is present as ContentStreamHash on the request instance, return
        /// the value to be used in request canonicalization. 
        /// If not set as a header or in the request, attempt to compute a hash based on
        /// inspection of the style of the request content.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>
        /// The computed hash, whether already set in headers or computed here. Null
        /// if we were not able to compute a hash.
        /// </returns>
        public static string SetRequestBodyHash(IRequest request)
        {
            string computedContentHash = null;
            if (request.Headers.TryGetValue(XAmzContentSha256, out computedContentHash))
                return computedContentHash;

            if (request.UseChunkEncoding)
            {
                computedContentHash = StreamingBodySha256;
                if (request.Headers.ContainsKey("Content-Length"))
                {
                    // substitute the originally declared content length with the true size of
                    // the data we'll upload, which is inflated with chunk metadata
                    request.Headers[XAmzDecodedContentLength] = request.Headers["Content-Length"];
                    var originalContentLength = long.Parse(request.Headers["Content-Length"], CultureInfo.InvariantCulture);
                    request.Headers["Content-Length"]
                        = ChunkedUploadWrapperStream.ComputeChunkedContentLength(originalContentLength).ToString(CultureInfo.InvariantCulture);
                }
                request.Headers["Content-Encoding"] = "aws-chunked";
            }
            else
            {
                if (request.ContentStream != null)
                    computedContentHash = request.ComputeContentStreamHash();
                else
                {
                    byte[] payloadHashBytes;
                    if (request.Content != null)
                        payloadHashBytes = CryptoUtilFactory.CryptoInstance.ComputeSHA256Hash(request.Content);
                    else
                    {
                        var payload = request.UseQueryString ? "" : GetRequestPayload(request);
                        payloadHashBytes = CryptoUtilFactory.CryptoInstance.ComputeSHA256Hash(Encoding.UTF8.GetBytes(payload));
                    }
                    computedContentHash = AWSSDKUtils.ToHex(payloadHashBytes, true);
                }
            }

            if (computedContentHash != null)
                request.Headers.Add(XAmzContentSha256, computedContentHash);

            return computedContentHash;
        }

        /// <summary>
        /// Returns the HMAC256 for an arbitrary blob using the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] SignBlob(byte[] key, string data)
        {
            return SignBlob(key, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Returns the HMAC256 for an arbitrary blob using the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] SignBlob(byte[] key, byte[] data)
        {
            return CryptoUtilFactory.CryptoInstance.HMACSignBinary(data, key, SignerAlgorithm);
        }

        /// <summary>
        /// Compute and return the hash of a data blob using the specified key
        /// </summary>
        /// <param name="algorithm">Algorithm to use for hashing</param>
        /// <param name="key">Hash key</param>
        /// <param name="data">Data blob</param>
        /// <returns>Hash of the data</returns>
        public static byte[] ComputeKeyedHash(SigningAlgorithm algorithm, byte[] key, string data)
        {
            return ComputeKeyedHash(algorithm, key, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Compute and return the hash of a data blob using the specified key
        /// </summary>
        /// <param name="algorithm">Algorithm to use for hashing</param>
        /// <param name="key">Hash key</param>
        /// <param name="data">Data blob</param>
        /// <returns>Hash of the data</returns>
        public static byte[] ComputeKeyedHash(SigningAlgorithm algorithm, byte[] key, byte[] data)
        {
            return CryptoUtilFactory.CryptoInstance.HMACSignBinary(data, key, algorithm);
        }

        /// <summary>
        /// Computes the non-keyed hash of the supplied data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] ComputeHash(string data)
        {
            return ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Computes the non-keyed hash of the supplied data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] ComputeHash(byte[] data)
        {
            return CryptoUtilFactory.CryptoInstance.ComputeSHA256Hash(data);
        }

        #endregion

        #region Private Signing Helpers

        internal static string DetermineRegion(ClientConfig clientConfig)
        {
            if (!string.IsNullOrEmpty(clientConfig.AuthenticationRegion))
                return clientConfig.AuthenticationRegion.ToLower(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(clientConfig.ServiceURL))
            {
                var parsedRegion = AWSSDKUtils.DetermineRegion(clientConfig.ServiceURL);
                if (!string.IsNullOrEmpty(parsedRegion))
                    return parsedRegion.ToLower(CultureInfo.InvariantCulture);
            }
            
            return clientConfig.RegionEndpoint != null 
                ? clientConfig.RegionEndpoint.SystemName 
                : string.Empty;
        }

        internal static string DetermineService(ClientConfig clientConfig)
        {
            return !string.IsNullOrEmpty(clientConfig.AuthenticationServiceName) 
                ? clientConfig.AuthenticationServiceName.ToLower(CultureInfo.InvariantCulture) 
                : AWSSDKUtils.DetermineService(clientConfig.DetermineServiceURL()).ToLower(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Computes and returns the canonical request
        /// </summary>
        /// <param name="resourcePath">the path of the resource being operated on</param>
        /// <param name="httpMethod">The http method used for the request</param>
        /// <param name="sortedHeaders">The full request headers, sorted into canonical order</param>
        /// <param name="canonicalQueryString">The query parameters for the request</param>
        /// <param name="precomputedBodyHash">
        /// The hash of the binary request body if present. If not supplied, the routine
        /// will look for the hash as a header on the request.
        /// </param>
        /// <returns>Canonicalised request as a string</returns>
        protected static string CanonicalizeRequest(string resourcePath, 
                                                    string httpMethod,
                                                    IDictionary<string, string> sortedHeaders,
                                                    string canonicalQueryString,
                                                    string precomputedBodyHash)
        {
            var canonicalRequest = new StringBuilder();
            canonicalRequest.AppendFormat("{0}\n", httpMethod);
            canonicalRequest.AppendFormat("{0}\n", CanonicalizeResourcePath(resourcePath));
            canonicalRequest.AppendFormat("{0}\n", canonicalQueryString);

            canonicalRequest.AppendFormat("{0}\n", CanonicalizeHeaders(sortedHeaders));
            canonicalRequest.AppendFormat("{0}\n", CanonicalizeHeaderNames(sortedHeaders));

            if (precomputedBodyHash != null)
            {
                canonicalRequest.Append(precomputedBodyHash);
            }
            else
            {
                string contentHash;
                if (sortedHeaders.TryGetValue(XAmzContentSha256, out contentHash))
                    canonicalRequest.Append(contentHash);
            }

            return canonicalRequest.ToString();
        }

        /// <summary>
        /// Returns the canonicalized resource path for the service endpoint
        /// </summary>
        /// <param name="resourcePath">Resource path for the request</param>
        /// <returns>Canonicalized resource path for the endpoint</returns>
        protected static string CanonicalizeResourcePath(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return "/";
            var canonicalizedPath = resourcePath.StartsWith("/", StringComparison.Ordinal) ? resourcePath : "/" + resourcePath;
            return AWSSDKUtils.UrlEncode(canonicalizedPath, true);
        }

        /// <summary>
        /// Reorders the headers for the request for canonicalization.
        /// </summary>
        /// <param name="requestHeaders">The set of proposed headers for the request</param>
        /// <returns>List of headers that must be included in the signature</returns>
        /// <remarks>For AWS4 signing, all headers are considered viable for inclusion</remarks>
        protected static IDictionary<string, string> SortHeaders(IEnumerable<KeyValuePair<string, string>> requestHeaders)
        {
            var canonicalizedHeaders = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in requestHeaders)
            {
                canonicalizedHeaders.Add(header.Key, header.Value);
            }
            return canonicalizedHeaders;
        }

        /// <summary>
        /// Computes the canonical headers with values for the request. Only headers included in the signature
        /// are included in the canonicalization process.
        /// </summary>
        /// <param name="sortedHeaders">All request headers, sorted into canonical order</param>
        /// <returns>Canonicalized string of headers, with the header names in lower case.</returns>
        protected static string CanonicalizeHeaders(ICollection<KeyValuePair<string, string>> sortedHeaders)
        {
            if (sortedHeaders == null || sortedHeaders.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var entry in sortedHeaders)
            {
                builder.Append(entry.Key.ToLower(CultureInfo.InvariantCulture));
                builder.Append(":");
                builder.Append(CompressSpaces(entry.Value));
                builder.Append("\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the set of headers included in the signature as a flattened, ;-delimited string
        /// </summary>
        /// <param name="sortedHeaders">The headers included in the signature</param>
        /// <returns>Formatted string of header names</returns>
        protected static string CanonicalizeHeaderNames(IEnumerable<KeyValuePair<string, string>> sortedHeaders)
        {
            var builder = new StringBuilder();
            foreach (var header in sortedHeaders)
            {
                if (builder.Length > 0)
                    builder.Append(";");
                builder.Append(header.Key.ToLower(CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        /// <summary>
        /// Computes and returns the canonicalized query string, if query parameters have been supplied.
        /// Parameters with no value will be canonicalized as 'param='. The expectation is that parameters
        /// have not already been url encoded prior to canonicalization.
        /// </summary>
        /// <param name="queryString">The set of parameters being passed on the uri</param>
        /// <param name="uriEncodeParameters">
        /// Parameters must be uri encoded into the canonical request and by default the signer expects
        /// that the supplied collection contains non-encoded data. Set this to false if the encoding was
        /// done prior to signer entry.
        /// </param>
        /// <returns>The uri encoded query string parameters in canonical ordering</returns>
        protected static string CanonicalizeQueryParameters(string queryString, bool uriEncodeParameters = true)
        {
            if (string.IsNullOrEmpty(queryString))
                return string.Empty;

            var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var queryParamsStart = queryString.IndexOf('?');
            var qs = queryString.Substring(++queryParamsStart); 
            int subStringPos = 0;
            int index = qs.IndexOfAny(new char[] { '&', ';' }, 0);
            if (index == -1 && subStringPos < qs.Length)
                index = qs.Length;
            while (index != -1)
            {
                string token = qs.Substring(subStringPos, index - subStringPos);

                // If the next character is a space then this isn't the end of query string value
                // Content Disposition is an example of this.
                if (!(index + 1 < qs.Length && qs[index + 1] == ' '))
                {
                    int equalPos = token.IndexOf('=');
                    if (equalPos == -1)
                        queryParams.Add(token, null);
                    else
                        queryParams.Add(token.Substring(0, equalPos), token.Substring(equalPos + 1));

                    subStringPos = index + 1;
                }

                if (qs.Length <= index + 1)
                    break;

                index = qs.IndexOfAny(new char[] { '&', ';' }, index + 1);
                if (index == -1 && subStringPos < qs.Length)
                    index = qs.Length;
            }

            return CanonicalizeQueryParameters(queryParams, uriEncodeParameters);
        }

        /// <summary>
        /// Computes and returns the canonicalized query string, if query parameters have been supplied.
        /// Parameters with no value will be canonicalized as 'param='. The expectation is that parameters
        /// have not already been url encoded prior to canonicalization.
        /// </summary>
        /// <param name="parameters">The set of parameters to be encoded in the query string</param>
        /// <param name="uriEncodeParameters">
        /// Parameters must be uri encoded into the canonical request and by default the signer expects
        /// that the supplied collection contains non-encoded data. Set this to false if the encoding was
        /// done prior to signer entry.
        /// </param>
        /// <returns>The uri encoded query string parameters in canonical ordering</returns>
        protected static string CanonicalizeQueryParameters(IDictionary<string, string> parameters, 
                                                            bool uriEncodeParameters = true)
        {
            if (parameters == null || parameters.Count == 0)
                return string.Empty;

            var canonicalQueryString = new StringBuilder();
            var queryParams = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal);
            foreach (var p in queryParams)
            {
                if (canonicalQueryString.Length > 0)
                    canonicalQueryString.Append("&");
                if (uriEncodeParameters)
                {
                    if (string.IsNullOrEmpty(p.Value))
                        canonicalQueryString.AppendFormat("{0}=", AWSSDKUtils.UrlEncode(p.Key, false));
                    else
                        canonicalQueryString.AppendFormat("{0}={1}", AWSSDKUtils.UrlEncode(p.Key, false), AWSSDKUtils.UrlEncode(p.Value, false));
                }
                else
                {
                    if (string.IsNullOrEmpty(p.Value))
                        canonicalQueryString.AppendFormat("{0}=", p.Key);
                    else
                        canonicalQueryString.AppendFormat("{0}={1}", p.Key, p.Value);
                }
            }

            return canonicalQueryString.ToString();
        }

        static string CompressSpaces(string data)
        {
            if (data == null || !data.Contains(" "))
                return data;

            var compressed = CompressWhitespaceRegex.Replace(data, " ");
            return compressed;
        }

        /// <summary>
        /// Returns the request parameters in the form of a query string.
        /// </summary>
        /// <param name="request">The request instance</param>
        /// <returns>Request parameters in query string format</returns>
        static string GetRequestPayload(IRequest request)
        {
            if (request.Content == null)
                return AWSSDKUtils.GetParametersAsString(request.Parameters);
            else
            {
                var encoding = Encoding.GetEncoding(DEFAULT_ENCODING);
                return encoding.GetString(request.Content, 0, request.Content.Length);
            }
        }

        #endregion
    }

    /// <summary>
    /// AWS4 protocol signer for Amazon S3 presigned urls.
    /// </summary>
    internal class AWS4PreSignedUrlSigner : AWS4Signer
    {
        // 7 days is the maximum period for presigned url expiry with AWS4
        public const Int64 MaxAWS4PreSignedUrlExpiry = 7 * 24 * 60 * 60;

        internal const string XAmzSignature = "X-Amz-Signature";
        internal const string XAmzAlgorithm = "X-Amz-Algorithm";
        internal const string XAmzCredential = "X-Amz-Credential";
        internal const string XAmzExpires = "X-Amz-Expires";

        public const string UnsignedPayload = "UNSIGNED-PAYLOAD";

        /// <summary>
        /// Calculates and signs the specified request using the AWS4 signing protocol by using the
        /// AWS account credentials given in the method parameters. The resulting signature is added
        /// to the request headers as 'Authorization'.
        /// </summary>
        /// <param name="request">
        /// The request to compute the signature for. Additional headers mandated by the AWS4 protocol 
        /// ('host' and 'x-amz-date') will be added to the request before signing.
        /// </param>
        /// <param name="clientConfig">
        /// Adding supporting data for the service call required by the signer (notably authentication
        /// region, endpoint and service name).
        /// </param>
        /// <param name="metrics">
        /// Metrics for the request
        /// </param>
        /// <param name="awsAccessKeyId">
        /// The AWS public key for the account making the service call.
        /// </param>
        /// <param name="awsSecretAccessKey">
        /// The AWS secret key for the account making the call, in clear text
        /// </param>
        /// <exception cref="Amazon.Runtime.SignatureException">
        /// If any problems are encountered while signing the request.
        /// </exception>
        public override void Sign(IRequest request,
                                  ClientConfig clientConfig,
                                  RequestMetrics metrics,
                                  string awsAccessKeyId,
                                  string awsSecretAccessKey)
        {
            throw new InvalidOperationException("PreSignedUrl signature computation is not supported by this method; use SignRequest instead.");
        }

        /// <summary>
        /// Calculates the AWS4 signature for a presigned url.
        /// </summary>
        /// <param name="request">
        /// The request to compute the signature for. Additional headers mandated by the AWS4 protocol 
        /// ('host' and 'x-amz-date') will be added to the request before signing. If the Expires parameter
        /// is present, it is renamed to 'X-Amz-Expires' before signing.
        /// </param>
        /// <param name="clientConfig">
        /// Adding supporting data for the service call required by the signer (notably authentication
        /// region, endpoint and service name).
        /// </param>
        /// <param name="metrics">
        /// Metrics for the request
        /// </param>
        /// <param name="awsAccessKeyId">
        /// The AWS public key for the account making the service call.
        /// </param>
        /// <param name="awsSecretAccessKey">
        /// The AWS secret key for the account making the call, in clear text
        /// </param>
        /// <exception cref="Amazon.Runtime.SignatureException">
        /// If any problems are encountered while signing the request.
        /// </exception>
        /// <remarks>
        /// Parameters passed as part of the resource path should be uri-encoded prior to
        /// entry to the signer. Parameters passed in the request.Parameters collection should
        /// be not be encoded; encoding will be done for these parameters as part of the 
        /// construction of the canonical request.
        /// </remarks>
        public new AWS4SigningResult SignRequest(IRequest request,
                                                 ClientConfig clientConfig,
                                                 RequestMetrics metrics,
                                                 string awsAccessKeyId,
                                                 string awsSecretAccessKey)
        {
            // clean up any prior signature in the headers if resigning
            request.Headers.Remove(AuthorizationHeader);
            if (!request.Headers.ContainsKey(HostHeader))
            {
                var hostHeader = request.Endpoint.Host;
                if (!request.Endpoint.IsDefaultPort)
                    hostHeader += ":" + request.Endpoint.Port;
                request.Headers.Add(HostHeader, hostHeader);
            }

            var signedAt = DateTime.UtcNow;
            var region = DetermineRegion(clientConfig);
            const string service = "s3";

            // remove any hash supplied in headers in favor of the 'unsigned-payload' expected by AWS4
            if (request.Headers.ContainsKey(XAmzContentSha256))
                request.Headers.Remove(XAmzContentSha256);
            var sortedHeaders = SortHeaders(request.Headers);
            var canonicalizedHeaderNames = CanonicalizeHeaderNames(sortedHeaders);

            // add the auth parameters for canonicalization to a copy of the parameters so we don't
            // end up adding url encoded data to the url subsquently
            var copiedParameters = new Dictionary<string, string>(request.Parameters)
                {
                    {XAmzAlgorithm, string.Format(CultureInfo.InvariantCulture, "{0}-{1}", Scheme, Algorithm)},
                    {
                        XAmzCredential, string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}",
                                                      awsAccessKeyId,
                                                      FormatDateTime(signedAt, AWSSDKUtils.ISO8601BasicDateFormat),
                                                      region,
                                                      service,
                                                      Terminator)
                    },
                    {XAmzDate, FormatDateTime(signedAt, AWSSDKUtils.ISO8601BasicDateTimeFormat)},
                    {XAmzSignedHeaders, canonicalizedHeaderNames}
                };

            var canonicalQueryParams = CanonicalizeQueryParameters(copiedParameters);

            var canonicalRequest = CanonicalizeRequest(request.ResourcePath,
                                                       request.HttpMethod,
                                                       sortedHeaders,
                                                       canonicalQueryParams,
                                                       UnsignedPayload);
            if (metrics != null)
                metrics.AddProperty(Metric.CanonicalRequest, canonicalRequest);

            return ComputeSignature(awsAccessKeyId,
                                    awsSecretAccessKey,
                                    region,
                                    signedAt,
                                    service,
                                    canonicalizedHeaderNames,
                                    canonicalRequest,
                                    metrics);
        }
    }

    /// <summary>
    /// Encapsulates the various fields and eventual signing value that makes up 
    /// an AWS4 signature. This can be used to retrieve the required authorization string
    /// or authorization query parameters for the final request as well as hold ongoing
    /// signature computations for subsequent calls related to the initial signing.
    /// </summary>
    public class AWS4SigningResult
    {
        private readonly string _awsAccessKeyId;
        private readonly DateTime _originalDateTime;
        private readonly string _signedHeaders; 
        private readonly string _scope;
        private readonly byte[] _signingKey;
        private readonly byte[] _signature;

        /// <summary>
        /// Constructs a new signing result instance for a computed signature
        /// </summary>
        /// <param name="awsAccessKeyId">The access key that was included in the signature</param>
        /// <param name="signedAt">Date/time (UTC) that the signature was computed</param>
        /// <param name="signedHeaders">The collection of headers names that were included in the signature</param>
        /// <param name="scope">Formatted 'scope' value for signing (YYYYMMDD/region/service/aws4_request)</param>
        /// <param name="signingKey">Returns the key that was used to compute the signature</param>
        /// <param name="signature">Computed signature</param>
        public AWS4SigningResult(string awsAccessKeyId, 
                                 DateTime signedAt, 
                                 string signedHeaders, 
                                 string scope, 
                                 byte[] signingKey, 
                                 byte[] signature)
        {
            this._awsAccessKeyId = awsAccessKeyId;
            this._originalDateTime = signedAt;
            this._signedHeaders = signedHeaders;
            this._scope = scope;
            this._signingKey = signingKey;
            this._signature = signature;
        }

        /// <summary>
        /// The access key that was used in signature computation.
        /// </summary>
        public string AccessKeyId
        {
            get { return _awsAccessKeyId; }
        }

        /// <summary>
        /// ISO8601 formatted date/time that the signature was computed
        /// </summary>
        public string ISO8601DateTime
        {
            get { return AWS4Signer.FormatDateTime(_originalDateTime, AWSSDKUtils.ISO8601BasicDateTimeFormat); }
        }

        /// <summary>
        /// ISO8601 formatted date that the signature was computed
        /// </summary>
        public string ISO8601Date
        {
            get { return AWS4Signer.FormatDateTime(_originalDateTime, AWSSDKUtils.ISO8601BasicDateFormat); }
        }

        /// <summary>
        /// The ;-delimited collection of header names that were included in the signature computation
        /// </summary>
        public string SignedHeaders
        {
            get
            {
                return _signedHeaders;                
            }
        }

        /// <summary>
        /// Formatted 'scope' value for signing (YYYYMMDD/region/service/aws4_request)
        /// </summary>
        public string Scope
        {
            get { return _scope; }
        }

        /// <summary>
        /// Returns a copy of the key that was used to compute the signature
        /// </summary>
        public byte[] SigningKey
        {
            get
            {
                var kSigningCopy = new byte[_signingKey.Length];
                _signingKey.CopyTo(kSigningCopy, 0);
                return kSigningCopy;
            }
        }

        /// <summary>
        /// Returns the hex string representing the signature
        /// </summary>
        public string Signature
        {
            get
            {
                return AWSSDKUtils.ToHex(_signature, true);
            }
        }

        /// <summary>
        /// Returns a copy of the byte array containing the signature
        /// </summary>
        public byte[] SignatureBytes
        {
            get
            {
                var signatureCopy = new byte[_signature.Length];
                _signature.CopyTo(signatureCopy, 0);
                return signatureCopy;
            }
        }

        /// <summary>
        /// Returns the signature in a form usable as an 'Authorization' header value.
        /// </summary>
        public string ForAuthorizationHeader
        {
            get
            {
                var authorizationHeader = new StringBuilder();
                authorizationHeader.AppendFormat("{0}-{1} ", AWS4Signer.Scheme, AWS4Signer.Algorithm);
                authorizationHeader.AppendFormat("{0}={1}/{2}, ", AWS4Signer.Credential, AccessKeyId, Scope);
                authorizationHeader.AppendFormat("{0}={1}, ", AWS4Signer.SignedHeaders, SignedHeaders);
                authorizationHeader.AppendFormat("{0}={1}", AWS4Signer.Signature, Signature);

                return authorizationHeader.ToString();
            }
        }

        /// <summary>
        /// Returns the signature in a form usable as a set of query string parameters.
        /// </summary>
        public string ForQueryParameters
        {
            get
            {
                var authParams = new StringBuilder();

                authParams.AppendFormat("{0}={1}",
                                        AWS4PreSignedUrlSigner.XAmzAlgorithm,
                                        string.Format(CultureInfo.InvariantCulture, "{0}-{1}", AWS4Signer.Scheme, AWS4Signer.Algorithm));
                authParams.AppendFormat("&{0}={1}",
                                        AWS4PreSignedUrlSigner.XAmzCredential,
                                        string.Format(CultureInfo.InvariantCulture, "{0}/{1}", AccessKeyId, Scope));
                authParams.AppendFormat("&{0}={1}", AWS4Signer.XAmzDate, ISO8601DateTime);
                authParams.AppendFormat("&{0}={1}", AWS4Signer.XAmzSignedHeaders, SignedHeaders);
                authParams.AppendFormat("&{0}={1}", AWS4PreSignedUrlSigner.XAmzSignature, Signature);

                return authParams.ToString();
            }
        }
    }
}
