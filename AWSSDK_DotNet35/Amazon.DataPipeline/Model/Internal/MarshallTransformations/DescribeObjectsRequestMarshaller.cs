/*
 * Copyright 2010-2014 Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

using Amazon.DataPipeline.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;
using Amazon.Runtime.Internal.Util;
using ThirdParty.Json.LitJson;

namespace Amazon.DataPipeline.Model.Internal.MarshallTransformations
{
    /// <summary>
    /// Describe Objects Request Marshaller
    /// </summary>       
    internal class DescribeObjectsRequestMarshaller : IMarshaller<IRequest, DescribeObjectsRequest> 
    {
        

        public IRequest Marshall(DescribeObjectsRequest describeObjectsRequest) 
        {

            IRequest request = new DefaultRequest(describeObjectsRequest, "AmazonDataPipeline");
            string target = "DataPipeline.DescribeObjects";
            request.Headers["X-Amz-Target"] = target;
            
            request.Headers["Content-Type"] = "application/x-amz-json-1.1";
            
            string uriResourcePath = ""; 
            request.ResourcePath = uriResourcePath;
            
             
            using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                JsonWriter writer = new JsonWriter(stringWriter);
                writer.WriteObjectStart();
                
                if (describeObjectsRequest != null && describeObjectsRequest.IsSetPipelineId()) 
                {
                    writer.WritePropertyName("pipelineId");
                    writer.Write(describeObjectsRequest.PipelineId);
                }

                if (describeObjectsRequest != null && describeObjectsRequest.ObjectIds != null && describeObjectsRequest.ObjectIds.Count > 0) 
                {
                    List<string> objectIdsList = describeObjectsRequest.ObjectIds;
                    writer.WritePropertyName("objectIds");
                    writer.WriteArrayStart();

                    foreach (string objectIdsListValue in objectIdsList) 
                    { 
                        writer.Write(StringUtils.FromString(objectIdsListValue));
                    }

                    writer.WriteArrayEnd();
                }
                if (describeObjectsRequest != null && describeObjectsRequest.IsSetEvaluateExpressions()) 
                {
                    writer.WritePropertyName("evaluateExpressions");
                    writer.Write(describeObjectsRequest.EvaluateExpressions);
                }
                if (describeObjectsRequest != null && describeObjectsRequest.IsSetMarker()) 
                {
                    writer.WritePropertyName("marker");
                    writer.Write(describeObjectsRequest.Marker);
                }

                writer.WriteObjectEnd();
                
                string snippet = stringWriter.ToString();
                request.Content = System.Text.Encoding.UTF8.GetBytes(snippet);
            }
        

            return request;
        }
    }
}
