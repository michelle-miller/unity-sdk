﻿/**
* Copyright 2015 IBM Corp. All Rights Reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*      http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*
* @author Richard Lyle (rolyle@us.ibm.com)
*/

using IBM.Watson.Logging;
using IBM.Watson.Connection;
using IBM.Watson.Utilities;
using MiniJSON;
using System;
using System.Collections.Generic;
using System.Text;
using FullSerializer;
using System.Net;
using System.Collections;
using System.IO;

namespace IBM.Watson.Services.v1
{
    public class Dialog
    {
        #region Public Types
        public class DialogEntry
        {
            public string dialog_id { get; set; }
            public string name { get; set; }
        };
        public class Dialogs
        {
            public DialogEntry [] dialogs { get; set; }
        };
        public delegate void OnGetDialogs( Dialogs dialogs );
        public delegate void OnUploadDialog( string dialog_id );
        public delegate byte [] LoadFileDelegate( string filename );

        public class Response
        {
            public string [] response { get; set; }
            public string input { get; set; }
            public int conversation_id { get; set; }
            public double confidence { get; set; }
            public int client_id { get; set; }
        };
        public delegate void OnConverse( Response resp );
        #endregion

        #region Public Properties
        /// <summary>
        /// Set this property to overload the internal file loading of this class.
        /// </summary>
        public LoadFileDelegate LoadFile { get; set; }
        #endregion

        #region Private Data
        private const string SERVICE_ID = "DialogV1";
        private static fsSerializer sm_Serializer = new fsSerializer();
        #endregion

        #region GetDialogs
        /// <summary>
        /// Grabs a list of all available dialogs from the service.
        /// </summary>
        /// <param name="callback">The callback to receive the list of dialogs.</param>
        /// <returns>Returns true if request has been sent.</returns>
        public bool GetDialogs(OnGetDialogs callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            RESTConnector connector = RESTConnector.GetConnector(SERVICE_ID, "/v1/dialogs");
            if (connector == null)
                return false;

            GetDialogsReq req = new GetDialogsReq();
            req.Callback = callback;
            req.OnResponse = OnGetDialogsResp;

            return connector.Send(req);
        }
        private class GetDialogsReq : RESTConnector.Request
        {
            public OnGetDialogs Callback { get; set; }
        };
        private void OnGetDialogsResp(RESTConnector.Request req, RESTConnector.Response resp)
        {
            Dialogs classifiers = new Dialogs();
            if (resp.Success)
            {
                try
                {
                    fsData data = null;
                    fsResult r = fsJsonParser.Parse(Encoding.UTF8.GetString(resp.Data), out data);
                    if (!r.Succeeded)
                        throw new WatsonException(r.FormattedMessages);

                    object obj = classifiers;
                    r = sm_Serializer.TryDeserialize(data, obj.GetType(), ref obj);
                    if (!r.Succeeded)
                        throw new WatsonException(r.FormattedMessages);
                }
                catch (Exception e)
                {
                    Log.Error("NLC", "GetDialogs Exception: {0}", e.ToString());
                    resp.Success = false;
                }
            }

            if (((GetDialogsReq)req).Callback != null)
                ((GetDialogsReq)req).Callback(resp.Success ? classifiers : null);
        }
        #endregion

        #region UploadDialog
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dialogName"></param>
        /// <param name="callback"></param>
        /// <param name="dialogFileName"></param>
        /// <returns></returns>
        public bool UploadDialog( string dialogName, OnUploadDialog callback, string dialogFileName = null )
        {
            if (string.IsNullOrEmpty(dialogName))
                throw new ArgumentNullException("dialogName");
            if (callback == null)
                throw new ArgumentNullException("callback");

            byte [] dialogData = null;
            if ( dialogFileName != null )
            {
                if ( LoadFile != null )
                    dialogData = LoadFile( dialogFileName );
                else
                    dialogData = System.IO.File.ReadAllBytes( dialogFileName );

                if ( dialogData == null )
                {
                    Log.Error( "Dialog", "Failed to load dialog file data {0}", dialogFileName );
                    return false;
                }
            }

            RESTConnector connector = RESTConnector.GetConnector(SERVICE_ID, "/v1/dialogs");
            if (connector == null)
                return false;

            UploadDialogReq req = new UploadDialogReq();
            req.Callback = callback;
            req.OnResponse = OnCreateDialogResp;
            req.Forms = new Dictionary<string, RESTConnector.Form>();
            req.Forms["name"] = new RESTConnector.Form( dialogName );
            req.Forms["file"] = new RESTConnector.Form( dialogData, Path.GetFileName( dialogFileName ) );

            return connector.Send(req);
        }
        private class UploadDialogReq : RESTConnector.Request
        {
            public OnUploadDialog Callback { get; set; }
        };
        private void OnCreateDialogResp(RESTConnector.Request req, RESTConnector.Response resp)
        {
            string id = null;
            if (resp.Success)
            {
                try
                {
                    IDictionary json = Json.Deserialize( Encoding.UTF8.GetString( resp.Data ) ) as IDictionary;
                    id = (string)json["id"];
                }
                catch (Exception e)
                {
                    Log.Error("NLC", "UploadDialog Exception: {0}", e.ToString());
                    resp.Success = false;
                }
            }

            if (((UploadDialogReq)req).Callback != null)
                ((UploadDialogReq)req).Callback( id );
        }
        #endregion

        #region Converse
        public bool Converse( string dialogId, string input, OnConverse callback, 
            int conversation_id = 0, int client_id = 0 )
        {
            if (string.IsNullOrEmpty(dialogId))
                throw new ArgumentNullException("dialogId");
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException("input");
            if (callback == null)
                throw new ArgumentNullException("callback");

            RESTConnector connector = RESTConnector.GetConnector(SERVICE_ID, "/v1/dialogs");
            if (connector == null)
                return false;

            ConverseReq req = new ConverseReq();
            req.Function = "/" + dialogId + "/conversation";
            req.Callback = callback;
            req.OnResponse = ConverseResp;
            req.Forms = new Dictionary<string, RESTConnector.Form>();
            req.Forms["input"] = new RESTConnector.Form( input );
            if ( conversation_id != 0 )
                req.Forms["conversation_id"] = new RESTConnector.Form( conversation_id );
            if ( client_id != 0 )
                req.Forms["client_id"] = new RESTConnector.Form( client_id );

            return connector.Send(req);
        }
        private class ConverseReq : RESTConnector.Request
        {
            public OnConverse Callback { get; set; }
        };
        private void ConverseResp(RESTConnector.Request req, RESTConnector.Response resp)
        {
            Response response = new Response();
            if (resp.Success)
            {
                try
                {
                    fsData data = null;
                    fsResult r = fsJsonParser.Parse(Encoding.UTF8.GetString(resp.Data), out data);
                    if (!r.Succeeded)
                        throw new WatsonException(r.FormattedMessages);

                    object obj = response;
                    r = sm_Serializer.TryDeserialize(data, obj.GetType(), ref obj);
                    if (!r.Succeeded)
                        throw new WatsonException(r.FormattedMessages);
                }
                catch (Exception e)
                {
                    Log.Error("NLC", "ConverseResp Exception: {0}", e.ToString());
                    resp.Success = false;
                }
            }

            if (((ConverseReq)req).Callback != null)
                ((ConverseReq)req).Callback(resp.Success ? response : null);
        }
        #endregion
    }
}