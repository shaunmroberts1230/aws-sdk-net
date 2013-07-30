/*
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
using System.Xml.Serialization;
using System.Text;
using System.IO;

namespace Amazon.SimpleWorkflow.Model
{
    /// <summary>
    /// <para> Provides details of the <c>ContinueAsNewWorkflowExecutionFailed</c> event. </para>
    /// </summary>
    public class ContinueAsNewWorkflowExecutionFailedEventAttributes
    {
        
        private string cause;
        private long? decisionTaskCompletedEventId;

        /// <summary>
        /// The cause of the failure. This information is generated by the system and can be useful for diagnostic purposes. <note>If <b>cause</b> is
        /// set to OPERATION_NOT_PERMITTED, the decision failed because it lacked sufficient permissions. For details and example IAM policies, see <a
        /// href="http://docs.aws.amazon.com/amazonswf/latest/developerguide/swf-dev-iam.html">Using IAM to Manage Access to Amazon SWF
        /// Workflows</a>.</note>
        ///  
        /// <para>
        /// <b>Constraints:</b>
        /// <list type="definition">
        ///     <item>
        ///         <term>Allowed Values</term>
        ///         <description>UNHANDLED_DECISION, WORKFLOW_TYPE_DEPRECATED, WORKFLOW_TYPE_DOES_NOT_EXIST, DEFAULT_EXECUTION_START_TO_CLOSE_TIMEOUT_UNDEFINED, DEFAULT_TASK_START_TO_CLOSE_TIMEOUT_UNDEFINED, DEFAULT_TASK_LIST_UNDEFINED, DEFAULT_CHILD_POLICY_UNDEFINED, OPERATION_NOT_PERMITTED</description>
        ///     </item>
        /// </list>
        /// </para>
        /// </summary>
        public string Cause
        {
            get { return this.cause; }
            set { this.cause = value; }
        }

        /// <summary>
        /// Sets the Cause property
        /// </summary>
        /// <param name="cause">The value to set for the Cause property </param>
        /// <returns>this instance</returns>
        public ContinueAsNewWorkflowExecutionFailedEventAttributes WithCause(string cause)
        {
            this.cause = cause;
            return this;
        }
            

        // Check to see if Cause property is set
        internal bool IsSetCause()
        {
            return this.cause != null;
        }

        /// <summary>
        /// The id of the <c>DecisionTaskCompleted</c> event corresponding to the decision task that resulted in the
        /// <c>ContinueAsNewWorkflowExecution</c> decision that started this execution. This information can be useful for diagnosing problems by
        /// tracing back the cause of events.
        ///  
        /// </summary>
        public long DecisionTaskCompletedEventId
        {
            get { return this.decisionTaskCompletedEventId ?? default(long); }
            set { this.decisionTaskCompletedEventId = value; }
        }

        /// <summary>
        /// Sets the DecisionTaskCompletedEventId property
        /// </summary>
        /// <param name="decisionTaskCompletedEventId">The value to set for the DecisionTaskCompletedEventId property </param>
        /// <returns>this instance</returns>
        public ContinueAsNewWorkflowExecutionFailedEventAttributes WithDecisionTaskCompletedEventId(long decisionTaskCompletedEventId)
        {
            this.decisionTaskCompletedEventId = decisionTaskCompletedEventId;
            return this;
        }
            

        // Check to see if DecisionTaskCompletedEventId property is set
        internal bool IsSetDecisionTaskCompletedEventId()
        {
            return this.decisionTaskCompletedEventId.HasValue;
        }
    }
}