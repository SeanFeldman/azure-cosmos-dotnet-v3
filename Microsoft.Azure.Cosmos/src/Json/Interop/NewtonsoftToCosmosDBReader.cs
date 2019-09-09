﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// IJsonReader adapter for a Newtonsoft reader meaning we get a newtonsoft reader for testing purposes.
    /// </summary>
    internal sealed class NewtonsoftToCosmosDBReader : Microsoft.Azure.Cosmos.Json.JsonReader
    {
        private readonly Newtonsoft.Json.JsonReader reader;

        private NewtonsoftToCosmosDBReader(Newtonsoft.Json.JsonReader reader)
            : base(true)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            this.reader = reader;
        }

        public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public override IReadOnlyList<byte> GetBinaryValue()
        {
            throw new NotImplementedException();
        }

        public override IReadOnlyList<byte> GetBufferedRawJsonToken()
        {
            throw new NotImplementedException();
        }

        public override float GetFloat32Value()
        {
            throw new NotImplementedException();
        }

        public override double GetFloat64Value()
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuidValue()
        {
            throw new NotImplementedException();
        }

        public override short GetInt16Value()
        {
            throw new NotImplementedException();
        }

        public override int GetInt32Value()
        {
            throw new NotImplementedException();
        }

        public override long GetInt64Value()
        {
            throw new NotImplementedException();
        }

        public override sbyte GetInt8Value()
        {
            throw new NotImplementedException();
        }

        public override Number64 GetNumberValue()
        {
            string numberString;
            object value = this.reader.Value;
            if (value is double)
            {
                numberString = ((double)value).ToString("R");
            }
            else
            {
                numberString = value.ToString();
            }

            return double.Parse(numberString);
        }

        public override string GetStringValue()
        {
            return this.reader.Value.ToString();
        }

        public override uint GetUInt32Value()
        {
            throw new NotImplementedException();
        }

        public override bool Read()
        {
            bool succesfullyRead = this.reader.Read();
            if (succesfullyRead)
            {
                switch (this.reader.TokenType)
                {
                    case Newtonsoft.Json.JsonToken.None:
                    case Newtonsoft.Json.JsonToken.StartConstructor:
                    case Newtonsoft.Json.JsonToken.EndConstructor:
                        throw new InvalidOperationException();
                    case Newtonsoft.Json.JsonToken.StartObject:
                        this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);
                        break;
                    case Newtonsoft.Json.JsonToken.StartArray:
                        this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);
                        break;
                    case Newtonsoft.Json.JsonToken.PropertyName:
                        this.JsonObjectState.RegisterToken(JsonTokenType.FieldName);
                        break;
                    case Newtonsoft.Json.JsonToken.Comment:
                    case Newtonsoft.Json.JsonToken.Raw:
                    case Newtonsoft.Json.JsonToken.String:
                    case Newtonsoft.Json.JsonToken.Date:
                    case Newtonsoft.Json.JsonToken.Bytes:
                        this.JsonObjectState.RegisterToken(JsonTokenType.String);
                        break;
                    case Newtonsoft.Json.JsonToken.Integer:
                    case Newtonsoft.Json.JsonToken.Float:
                        this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                        break;
                    case Newtonsoft.Json.JsonToken.Boolean:
                        this.JsonObjectState.RegisterToken(this.reader.Value.ToString() == true.ToString() ? JsonTokenType.True : JsonTokenType.False);
                        break;
                    case Newtonsoft.Json.JsonToken.Null:
                    case Newtonsoft.Json.JsonToken.Undefined:
                        this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                        break;
                    case Newtonsoft.Json.JsonToken.EndObject:
                        this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                        break;
                    case Newtonsoft.Json.JsonToken.EndArray:
                        this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                        break;
                    default:
                        throw new ArgumentException("Got an invalid newtonsoft type");
                }
            }

            return succesfullyRead;
        }

        public static NewtonsoftToCosmosDBReader CreateFromString(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            StringReader stringReader = new StringReader(json);
            Newtonsoft.Json.JsonTextReader newtonsoftReader = new Newtonsoft.Json.JsonTextReader(stringReader);
            return new NewtonsoftToCosmosDBReader(newtonsoftReader);
        }

        public static NewtonsoftToCosmosDBReader CreateFromReader(Newtonsoft.Json.JsonReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return new NewtonsoftToCosmosDBReader(reader);
        }
    }
}
