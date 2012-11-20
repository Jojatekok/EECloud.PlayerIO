﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using EECloud.PlayerIO.Messages;
using ProtoBuf;

namespace EECloud.PlayerIO
{
    internal class HttpChannel
    {
        private const string EndpointUri = "http://api.playerio.com/api";
        private Dictionary<string, string> _headers;

        public HttpChannel()
        {

        }

        public TResponse Request<TRequest, TResponse, TError>(int method, TRequest args) where TError : Exception
        {
            var r = default(TResponse);

            var request = GetRequest(method);
			var requestStream = request.GetRequestStream();
			using (requestStream)
			{
				Serializer.Serialize(requestStream, args);
			}
			try
			{
				var response = request.GetResponse();
				var responseStream = response.GetResponseStream();
				using (responseStream)
				{
					if (ReadHeader(responseStream))
					{
						r = Serializer.Deserialize<TResponse>(responseStream);
					}
					else
					{
						throw GetError<TError>(responseStream);
					}
				}
			}
			catch (WebException webException1)
			{
				var webException = webException1;
				if (webException.Response == null)
				{
					throw new PlayerIOError(ErrorCode.GeneralError, "Connection to Player.IO WebService Unexpectly Terminated");
				}

			    var stream = webException.Response.GetResponseStream();
			    using (stream)
			    {
			        if (stream != null)
			            using (var streamReader = new StreamReader(stream))
			            {
			                throw new PlayerIOError(ErrorCode.GeneralError, string.Concat("Error communicating with Player.IO WebService: ", streamReader.ReadToEnd()));
			            }
			    }
			}
			return r;
		}

        private WebRequest GetRequest(int method)
        {
            var value = WebRequest.Create(string.Concat(EndpointUri, "/", method));
            value.Timeout = 15000;
            value.Method = "POST";
            if (_headers != null)
            {
                lock (_headers)
                {
                    foreach (var header in _headers)
                    {
                        value.Headers[header.Key] = header.Value;
                    }
                }
            }
            return value;
        }

        private bool ReadHeader(Stream responseStream)
        {
            if (responseStream.ReadByte() == 1)
            {
                var num = (ushort)(responseStream.ReadByte() << 8 | responseStream.ReadByte());
                var numArray = new byte[num];
                responseStream.Read(numArray, 0, numArray.Length);
                lock (_headers)
                {
                    _headers["playertoken"] = Encoding.UTF8.GetString(numArray, 0, numArray.Length);
                }
            }
            return responseStream.ReadByte() == 1;
        }

        public static TError GetError<TError>(Stream errorStream) where TError : Exception
        {
            if (typeof(TError) != typeof(PlayerIOError))
            {
                if (typeof(TError) != typeof(PlayerIORegistrationError))
                {
                    return new ApplicationException(string.Concat("Unexpected error type: ", typeof(TError))) as TError;
                }
                var regError = Serializer.Deserialize<RegistrationError>(errorStream);
                return new PlayerIORegistrationError(regError.ErrorCode, regError.Message, regError.UsernameError, regError.PasswordError, regError.EmailError, regError.CaptchaError) as TError;
            }
            var err = Serializer.Deserialize<Error>(errorStream);
            return new PlayerIOError(err.ErrorCode, err.Message) as TError;
        }

        public void SetToken(string token)
        {
            var strs = new Dictionary<string, string>();
            strs["playertoken"] = token;
            _headers = strs;
        }

    }
}