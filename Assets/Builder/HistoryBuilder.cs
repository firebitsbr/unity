using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PubNubAPI
{
    public class HistoryBuilder: PubNubNonSubBuilder<HistoryBuilder>, IPubNubNonSubscribeBuilder<HistoryBuilder, PNHistoryResult>
    {
        public string HistoryChannel { get; private set;}
        public long StartTime { get; private set;}
        public long EndTime { get; private set;}
        private ushort count = 100;
        private const ushort MaxCount = 100;
        public ushort HistoryCount { 
            get {
                return count;
            } 
            private set {
                if(value > 100 || value <= 0){ 
                    count = 100; 
                } else {
                    count = value;
                }
            }
        }
        private Action<PNHistoryResult, PNStatus> Callback;
        public bool ReverseHistory { get; private set;}
        public bool IncludeTimetokenInHistory { get; private set;}
        public HistoryBuilder(PubNubUnity pn): base(pn){
            Debug.Log ("HistoryBuilder Construct");
        }

        public HistoryBuilder IncludeTimetoken(bool includeTimetoken){
            IncludeTimetokenInHistory = includeTimetoken;
            return this;
        }

        public HistoryBuilder Reverse(bool reverse){
            ReverseHistory = reverse;
            return this;
        }

        public HistoryBuilder Start(long start){
            StartTime = start;
            return this;
        }

        public HistoryBuilder End(long end){
            EndTime = end;
            return this;
        }

        public HistoryBuilder Channel(string channel){
            HistoryChannel = channel;
            return this;
        }

        public HistoryBuilder Count(ushort count){
            HistoryCount = count;
            return this;
        }

        #region IPubNubBuilder implementation

        public void Async(Action<PNHistoryResult, PNStatus> callback)
        {
            //TODO: Add history channel check
            Callback = callback;
            Debug.Log ("PNHistoryBuilder Async");
            base.Async<PNHistoryResult>(callback, PNOperationType.PNHistoryOperation, CurrentRequestType.NonSubscribe, this);
        }

         protected override void RunWebRequest(QueueManager qm){

            RequestState<PNHistoryResult> requestState = new RequestState<PNHistoryResult> ();
            requestState.RespType = PNOperationType.PNHistoryOperation;

            Debug.Log ("HistoryBuilder Channel: " + this.HistoryChannel);
            Debug.Log ("HistoryBuilder Channel: " + this.StartTime);
            Debug.Log ("HistoryBuilder Channel: " + this.EndTime);
            Debug.Log ("HistoryBuilder Channel: " + this.HistoryCount);


            Uri request = BuildRequests.BuildHistoryRequest(
                this.HistoryChannel,
                this.StartTime,
                this.EndTime,
                this.HistoryCount,
                this.ReverseHistory,
                this.IncludeTimetokenInHistory,
                this.PubNubInstance.PNConfig.UUID,
                this.PubNubInstance.PNConfig.Secure,
                this.PubNubInstance.PNConfig.Origin,
                this.PubNubInstance.PNConfig.AuthKey,
                this.PubNubInstance.PNConfig.SubscribeKey,
                this.PubNubInstance.Version
            );
            this.PubNubInstance.PNLog.WriteToLog(string.Format("RunHistoryRequest {0}", request.OriginalString), PNLoggingMethod.LevelInfo);
            base.RunWebRequest<PNHistoryResult>(qm, request, requestState, this.PubNubInstance.PNConfig.NonSubscribeTimeout, 0, this); 

        }

        protected override void CreatePubNubResponse(object deSerializedResult){
            PNHistoryResult pnHistoryResult = new PNHistoryResult();
            pnHistoryResult.Messages = new List<PNHistoryItemResult>();

            PNStatus pnStatus = new PNStatus();
            try{
                List<object> result = ((IEnumerable)deSerializedResult).Cast<object> ().ToList ();
                if(result != null){

                    var historyResponseArray = (from item in result
                            select item as object).ToArray ();
                    foreach(var h in historyResponseArray){
                        Debug.Log(h.ToString());
                    }

                    if(historyResponseArray.Length >= 1){
                        //TODO add checks
                        ExtractMessages(historyResponseArray, ref pnHistoryResult);
                    }

                    if(historyResponseArray.Length > 1){
                        pnHistoryResult.StartTimetoken = Utility.ValidateTimetoken(historyResponseArray[1].ToString(), true);
                        Debug.Log(pnHistoryResult.StartTimetoken);
                    }

                    if(historyResponseArray.Length > 2){
                        pnHistoryResult.EndTimetoken = Utility.ValidateTimetoken(historyResponseArray[2].ToString(), true);
                        Debug.Log(pnHistoryResult.EndTimetoken);
                    }
                }
            } catch (Exception ex) {
                Debug.Log(ex.ToString());
                //throw ex;
            }
            Callback(pnHistoryResult, pnStatus);

        }

         private void ExtractMessageWithTimetokens( object element, string cipherKey, out PNHistoryItemResult pnHistoryItemResult){
            //[[{"message":{"text":"hey"},"timetoken":14985452911089049}],14985452911089049,14985452911089049] 
            //[[{"message":{"text":"hey"},"timetoken":14986549102032676},{"message":"E8VOcbfrYqLyHMtoVGv9UQ==","timetoken":14986619049105442},{"message":"E8VOcbfrYqLyHMtoVGv9UQ==","timetoken":14986619291068634}],14986549102032676,14986619291068634]
            pnHistoryItemResult = new PNHistoryItemResult();
            Dictionary<string, object> historyMessage = element as Dictionary<string, object>;
            Debug.Log("historyMessage" + historyMessage.ToString());
            object v;
            historyMessage.TryGetValue("message", out v);
            if(!string.IsNullOrEmpty(cipherKey) && (cipherKey.Length > 0)){
                //TODO: handle exception
                pnHistoryItemResult.Entry = Helpers.DecodeMessage(cipherKey, v, this.PubNubInstance.JsonLibrary);
            } else {
                pnHistoryItemResult.Entry = v;
            }
            Debug.Log(" v "+pnHistoryItemResult.Entry);

            object t;
            historyMessage.TryGetValue("timetoken", out t);
            pnHistoryItemResult.Timetoken = Utility.ValidateTimetoken(t.ToString(), false);
            Debug.Log(" t " + t.ToString());
            
        }

        private void ExtractMessage( object element, string cipherKey, out PNHistoryItemResult pnHistoryItemResult){
            //[[{"text":"hey"}],14985452911089049,14985452911089049]
            //[[{"text":"hey"},"E8VOcbfrYqLyHMtoVGv9UQ==","E8VOcbfrYqLyHMtoVGv9UQ=="],14986549102032676,14986619291068634]
            pnHistoryItemResult = new PNHistoryItemResult();
            if(!string.IsNullOrEmpty(cipherKey) && (cipherKey.Length > 0)){
                //TODO: handle exception
                pnHistoryItemResult.Entry = Helpers.DecodeMessage(cipherKey, element, this.PubNubInstance.JsonLibrary);
            } else {
                pnHistoryItemResult.Entry = element;
            }
            Debug.Log(" v "+pnHistoryItemResult.Entry);
        }

        private void ExtractMessages(object[] historyResponseArray, ref PNHistoryResult pnHistoryResult){
            IEnumerable enumerable = historyResponseArray [0] as IEnumerable;
            if (enumerable != null) {
                Debug.Log("enumerable" + enumerable.ToString());
                foreach (object elem in enumerable) {
                    var element = elem;
                    Debug.Log("element:" + element.ToString());
                    PNHistoryItemResult pnHistoryItemResult;

                    if(this.IncludeTimetokenInHistory){
                        ExtractMessageWithTimetokens(element, this.PubNubInstance.PNConfig.CipherKey, out pnHistoryItemResult);
                    } else {
                        ExtractMessage(element, this.PubNubInstance.PNConfig.CipherKey, out pnHistoryItemResult);
                    }
                    pnHistoryResult.Messages.Add(pnHistoryItemResult);
                }
            }
        }
        #endregion
    }
}

