/* BasicPlugin.cs

by PapaCharlie9@gmail.com

Free to use as is in any way you want with no warranty.

*/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    public class BFJC_Balancer : PRoConPluginAPI, IPRoConPluginInterface
    {

        /* Inherited:
            this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
            this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
        */

        #region 定数
        /// <summary>
        /// チームID：ニュートラル
        /// </summary>
        public const int TEAM_ID_NEUTRAL = 0;
        /// <summary>
        /// チームID：チーム１
        /// </summary>
        public const int TEAM_ID_1 = 1;
        /// <summary>
        /// チームID：チーム２
        /// </summary>
        public const int TEAM_ID_2 = 2;
        #endregion

        #region メンバ
        //private bool fIsEnabled;
        private int fDebugLevel;
        
        /// <summary>
        /// バーチャルモード
        /// </summary>
        private bool virtualMode;

        /// <summary>
        /// シャッフルディレイタイム
        /// </summary>
        private int shuffleDelayTime;

        /// <summary>
        /// チーム移動を許可するプレイヤー数（これ未満の人数の場合、チーム移動を許可する）
        /// </summary>
        public int allowTeamChangePlayerCount;
        
        /// <summary>
        /// 乱数生成オブジェクト
        /// </summary>
        private Random randomGenerator;

        /// <summary>
        /// プレイヤーリスト更新通知イベントハンドル
        /// </summary>
        private EventWaitHandle playerListEventHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        /// <summary>
        /// サーバの情報
        /// </summary>
        private Server serverInfo = new Server();

        /// <summary>
        /// ラウンド情報
        /// </summary>
        private Round roundInfo = new Round();
        #endregion

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BFJC_Balancer()
        {
            //fIsEnabled = false;
            fDebugLevel = 2;

            this.virtualMode = true;
            this.shuffleDelayTime = 30;
            this.allowTeamChangePlayerCount = 32;
            this.randomGenerator = new Random();
        }

        public enum MessageType { Warning, Error, Exception, Normal };

        public String FormatMessage(String msg, MessageType type)
        {
            String prefix = "[^b" + GetPluginName() + "^n] ";

            if (type.Equals(MessageType.Warning))
                prefix += "^1^bWARNING^0^n: ";
            else if (type.Equals(MessageType.Error))
                prefix += "^1^bERROR^0^n: ";
            else if (type.Equals(MessageType.Exception))
                prefix += "^1^bEXCEPTION^0^n: ";

            return prefix + msg;
        }


        public void LogWrite(String msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(String msg, MessageType type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(String msg)
        {
            ConsoleWrite(msg, MessageType.Normal);
        }

        public void ConsoleWarn(String msg)
        {
            ConsoleWrite(msg, MessageType.Warning);
        }

        public void ConsoleError(String msg)
        {
            ConsoleWrite(msg, MessageType.Error);
        }

        public void ConsoleException(String msg)
        {
            ConsoleWrite(msg, MessageType.Exception);
        }

        public void DebugWrite(String msg, int level)
        {
            if (fDebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
        }


        public void ServerCommand(params String[] args)
        {
            List<String> list = new List<String>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }


        public String GetPluginName()
        {
            return "BFJC-Balancer";
        }

        public String GetPluginVersion()
        {
            return "0.0.4";
        }

        public String GetPluginAuthor()
        {
            return "Aogik";
        }

        public String GetPluginWebsite()
        {
            return "bf.jpcommunity.com/";
        }

        public String GetPluginDescription()
        {
            return @"
<h2>Description</h2>
<p>This Plugin control Team Balance. (Beta Version)</p>

<h2>Settings</h2>
<p>Virtual Mode: Running in virtual mode. (True/False) </p>
<p>Shuffle Delay Time: Interval of time to shuffle. (Sec) </p>
<p>Allow TeamChange Time: Time from the start of round team that can be moved. (Sec) </p>

<h2>Development</h2>
<p>Battlefield JP Community</p>

<h3>Changelog</h3>
<blockquote><h4>0.0.4 (2013/12/15)</h4>
    - Fixed Bug<br/>
</blockquote>
<blockquote><h4>0.0.3 (2013/12/14)</h4>
    - Add Allow TeamChange PlayerCount Option<br/>
</blockquote>
<blockquote><h4>0.0.2 (2013/11/29)</h4>
	- Add Team Balance on player join<br/>
    - Add Allow TeamChange Time Option<br/>
</blockquote>
<blockquote><h4>0.0.1 (2013/11/23)</h4>
	- initial version<br/>
</blockquote>
";
        }


        public List<CPluginVariable> GetDisplayPluginVariables()
        {

            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            lstReturn.Add(new CPluginVariable("Settings|Debug level", fDebugLevel.GetType(), fDebugLevel));

            lstReturn.Add(new CPluginVariable("Shuffle Settings|Virtual Mode", virtualMode.GetType(), virtualMode));
            lstReturn.Add(new CPluginVariable("Shuffle Settings|Shuffle Delay Time", shuffleDelayTime.GetType(), shuffleDelayTime));
            lstReturn.Add(new CPluginVariable("Shuffle Settings|Allow TeamChange Time", this.roundInfo.AllowTeamChangeTime.GetType(), this.roundInfo.AllowTeamChangeTime));
            lstReturn.Add(new CPluginVariable("Shuffle Settings|Allow TeamChange PlayerCount", this.allowTeamChangePlayerCount.GetType(), this.allowTeamChangePlayerCount));

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (Regex.Match(strVariable, @"Debug level").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                fDebugLevel = tmp;
            }

            if (Regex.Match(strVariable, @"Virtual Mode").Success)
            {
                bool tmp = true;
                Boolean.TryParse(strValue, out tmp);
                virtualMode = tmp;

                ConsoleWrite("(Settings) Virtual Mode = " + virtualMode);
            }

            if (Regex.Match(strVariable, @"Shuffle Delay Time").Success)
            {
                int tmp = 30;
                int.TryParse(strValue, out tmp);
                shuffleDelayTime = tmp;

                ConsoleWrite("(Settings) Shuffle Delay Time = " + shuffleDelayTime);
            }

            if (Regex.Match(strVariable, @"Allow TeamChange Time").Success)
            {
                int tmp = 45;
                int.TryParse(strValue, out tmp);
                this.roundInfo.AllowTeamChangeTime = tmp;

                ConsoleWrite("(Settings) Allow TeamChange Time = " + this.roundInfo.AllowTeamChangeTime);
            }

            if (Regex.Match(strVariable, @"Allow TeamChange PlayerCount").Success)
            {
                int tmp = 32;
                int.TryParse(strValue, out tmp);
                this.allowTeamChangePlayerCount = tmp;

                ConsoleWrite("(Settings) Allow TeamChange PlayerCount = " + this.allowTeamChangePlayerCount);
            }
        }


        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            //this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
            //this.RegisterEvents(this.GetType().Name, "OnServerInfo", "OnListPlayers", "OnPlayerJoin", "OnPlayerSpawned", "OnPlayerTeamChange", "OnRoundOver", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded", "OnPlayerMovedByAdmin");
            this.RegisterEvents(this.GetType().Name, "OnServerInfo", "OnListPlayers", "OnPlayerTeamChange", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
        }

        //public override void OnGameAdminPlayerAdded(string soldierName)
        //{
        //    ConsoleWrite("OnGameAdminPlayerAdded!!!!! soldierName=" + soldierName);
        //}
        //public override void OnPlayerAuthenticated(string soldierName, string guid)
        //{
        //    ConsoleWrite("OnPlayerAuthenticated!!!!! soldierName=" + soldierName + " / guid=" + guid);
        //}

        //public override void OnTeamBalance(bool isEnabled)
        //{
        //    ConsoleWrite("OnTeamBalance!!!!! " + isEnabled);
        //}

        public void OnPluginEnable()
        {
            // ラウンド開始(プラグイン有効時)
            this.roundInfo.Start();

            //fIsEnabled = true;
            ConsoleWrite("Enabled!");
        }

        public void OnPluginDisable()
        {
            // ラウンド終了(プラグイン無効時)
            this.roundInfo.End();

            //fIsEnabled = false;
            ConsoleWrite("Disabled!");
        }
        
        public override void OnVersion(String serverType, String version) { }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            // サーバ情報保持
            this.serverInfo.ServerInfo = serverInfo;

            //ConsoleWrite("OnServerInfo: Debug level = " + fDebugLevel);
        }

        public override void OnResponseError(List<String> requestWords, String error) { }

        /// <summary>
        /// プレイヤーリスト受信イベント
        /// </summary>
        /// <param name="players"></param>
        /// <param name="subset"></param>
        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            // サーバから受信したプレイヤーリストを保存
            this.serverInfo.PlayerInfoList = new List<CPlayerInfo>(players);

            // 完了イベント通知
            this.playerListEventHandle.Set();

            ConsoleWrite("OnListPlayer: PlayerCount = " + this.serverInfo.PlayerInfoList.Count.ToString());
        }

        /*
         * 一度に大量にJOINしてくると、チームバランスが偏る不具合があるので、一旦停止
         * あと、司令官がJOINしてきた場合の挙動が不明
         */
        public override void OnPlayerJoin(String soldierName)
        {
            //new Thread(new ThreadStart(() =>
            //{
            //    // サーバから最新のプレイヤーリストを取得
            //    List<CPlayerInfo> playerList = getServerPlayersListSync();
            //    if (playerList != null)
            //    {
            //        // ニュートラルの人数
            //        int neutralcount = playerList.FindAll((p) => p.TeamID == TEAM_ID_NEUTRAL).Count;
            //        // チーム1の人数
            //        int team1count = playerList.FindAll((p) => p.TeamID == TEAM_ID_1).Count;
            //        // チーム2の人数
            //        int team2count = playerList.FindAll((p) => p.TeamID == TEAM_ID_2).Count;

            //        ConsoleWrite("OnPlayerJoin: Neutralcount = " + neutralcount + " / Team1Count = " + team1count + " / Team2Count = " + team2count);

            //        // ニュートラルの人数から、予想されるチームの人数を計算
            //        for (int i = 0; i < neutralcount; i++)
            //        {
            //            if (team1count <= team2count) { team1count++; }
            //            else { team2count++; }
            //        }

            //        // 人数が少ないチームへJOINさせる
            //        if (team1count == team2count)
            //        {
            //            // チームが同数の場合、ランダムにどちらかへJOINさせる
            //            int randomTeamId = this.randomGenerator.Next(TEAM_ID_1, TEAM_ID_2 + 1);
            //            MovePlayer(soldierName, randomTeamId, 0, true);
            //            ConsoleWrite("=====> " + soldierName + " JOINING TEAM" + randomTeamId + " (RANDOM)");
            //        }
            //        else if (team1count < team2count)
            //        {
            //            // チーム1へ強制JOIN
            //            MovePlayer(soldierName, TEAM_ID_1, 0, true);
            //            ConsoleWrite("=====> " + soldierName + " JOINING TEAM1");
            //        }
            //        else
            //        {
            //            // チーム2へ強制JOIN
            //            MovePlayer(soldierName, TEAM_ID_2, 0, true);
            //            ConsoleWrite("=====> " + soldierName + " JOINING TEAM2");
            //        }
            //    }
            //})).Start();
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo) { }

        public override void OnPlayerKilled(Kill kKillerVictimDetails) { }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) { }

        public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled)
        {
            ConsoleWrite("OnPlayerMovedByAdmin : " + soldierName + " / destinationTeamId=" + destinationTeamId + " / destinationSquadId=" + destinationSquadId + " / forceKilled=" + forceKilled);

            EventWaitHandle eventWaitHandle;
            string movePlayerKey = soldierName + destinationTeamId;
            if (this.roundInfo.AdminMovePlayerEventHandle.TryGetValue(movePlayerKey, out eventWaitHandle))
            {
                // アドミンによるチーム移動であることを通知する
                eventWaitHandle.Set();

                ConsoleWrite("Admin move player [" + soldierName +"]");
            }
        }

        public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId)
        {
            ConsoleWrite("OnPlayerTeamChange : " + soldierName + " / teamId = " + teamId + " / squadId=" + squadId + " / RoundTime=" + this.roundInfo.Time.ElapsedMilliseconds + "msec");

            string movePlayerKey = soldierName + teamId;
            if (this.roundInfo.AdminMovePlayers.Contains(movePlayerKey))
            {
                ConsoleWrite("RemoveForceMovePlayer: key=" + movePlayerKey + " / count=" + this.roundInfo.AdminMovePlayers.Count);

                // アドミンによるチーム移動 (元のチームに戻さない)
                this.roundInfo.RemoveForceMovePlayer(movePlayerKey);

                ConsoleWrite("Don't move again [" + soldierName + "]. (AdminForceMove)");
            }
            else
            {
                // プレイヤーによるチーム移動
                if (!IsAllowTeamChange()) // チーム移動可能か？
                {
                    // サーバJOIN時は、自動的にニュートラルからチーム移動されるので、その時は除外する
                    CPlayerInfo player = this.serverInfo.PlayerInfoList.Find((p) => p.SoldierName == soldierName);
                    if (player == null || player.TeamID == TEAM_ID_NEUTRAL)
                    {
                        ConsoleWrite("Neutral to Team move by Server. (PlayerJoin)");

                        // 少数チームへJOINさせる
                        BalancingJoinPlayer(soldierName);
                    }
                    else
                    {
                        // 元のチームへ戻す
                        AdminForceMovePlayer(soldierName, getOpposingTeamId(teamId), 0, true);

                        ConsoleWrite("<===== [" + soldierName + "] has been moved by Admin automatically. (PlayerTeamSwitch)");
                        SendGlobalMessage(soldierName + " has been moved by Admin automatically. (TeamSwitch)");
                    }
                }
            }
        }

        /* 
         * スレッドバージョン
         */ 
        //public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId)
        //{
        //    if (!IsAllowTeamChange()) // チーム移動不可？
        //    {
        //        string movePlayerKey = soldierName + getOpposingTeamId(teamId);

        //        EventWaitHandle eventWaitHandle;
        //        if (!this.roundInfo.AdminMovePlayerEventHandle.TryGetValue(movePlayerKey, out eventWaitHandle))
        //        {
        //            eventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        //            this.roundInfo.AdminMovePlayerEventHandle.Add(movePlayerKey, eventWaitHandle);
        //        }

        //        new Thread(new ThreadStart(() =>
        //        {
        //            Stopwatch sw = new Stopwatch();
        //            sw.Start();

        //            // OnPlayerMovedByAdminイベントが来ないか待って確認する
        //            int adminMoveEventWaitTime = 5000;// 5sec
        //            eventWaitHandle.WaitOne(adminMoveEventWaitTime);

        //            // 5秒待ってOnPlayerMovedByAdminイベントが来ない場合、プレイヤーによるチーム移動と判断する
        //            if (sw.ElapsedMilliseconds <= adminMoveEventWaitTime)
        //            {
        //                // アドミンによるチーム移動 (元のチームに戻さない)
        //                ConsoleWrite("Don't move again player [" + soldierName + "]. (Admin force move back)");
        //            }
        //            else
        //            {
        //                // プレイヤーによるチーム移動 (元のチームへ戻す)
        //                MovePlayer(soldierName, getOpposingTeamId(teamId), 0, true);

        //                ConsoleWrite("<===== [" + soldierName + "] has been moved by Admin. (TeamSwitch)");
        //            }

        //        })).Start();
        //    }
        //}

        public override void OnGlobalChat(String speaker, String message) { }

        public override void OnTeamChat(String speaker, String message, int teamId) { }

        public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players) { }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores)
        {
            string message = "OnRoundOverTeamScores:";
            foreach (TeamScore teamScore in teamScores)
            {
                message += " Team" + teamScore.TeamID + "=" + teamScore.Score;
            }
            message += " TIME=" + this.roundInfo.Time.Elapsed.Minutes + "m" + this.roundInfo.Time.Elapsed.Seconds + "s";
            ConsoleWrite(message);

            // ラウンドの終了通知
            this.roundInfo.End();

            // チームのシャッフル実行
            Thread shuffleThread = new Thread(new ThreadStart(Shuffle));
            shuffleThread.Start();
        }

        public override void OnRoundOver(int winningTeamId){ }

        public override void OnLoadingLevel(String mapFileName, int roundsPlayed, int roundsTotal)
        {
            ConsoleWrite("OnLoadingLevel");
        }

        public override void OnLevelStarted()
        {
            ConsoleWrite("OnLevelStarted");
        }

        public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal)
        {
            ConsoleWrite("OnLevelLoaded: (" + mapFileName + ") GuardTime=" + this.roundInfo.AllowTeamChangeTime + "sec");

            // ラウンドの開始通知
            this.roundInfo.Start();
        } // BF3

        /// <summary>
        /// チームシャッフル
        /// </summary>
        private void Shuffle()
        {
            try
            {
                ConsoleWrite("(Shuffle Thread) Shuffle Start !! Waiting " + shuffleDelayTime + "sec ...");
                System.Threading.Thread.Sleep(this.shuffleDelayTime * 1000);// wait

                // 最新のプレイヤーリストをサーバから取得
                List<CPlayerInfo> playerList = getServerPlayersListSync();
                if (playerList == null)
                {
                    ConsoleWrite("(Shuffle Thread) PlayerList is null !! Can't execute process.");
                    return;
                }
                ConsoleWrite("(Shuffle Thread) PlayerList Count=" + playerList.Count.ToString());

                // ランダムにソート
                List<CPlayerInfo> shuffleList = new List<CPlayerInfo>(playerList);
                shuffleList.Sort(SortByRandom);

                List<CPlayerInfo> team1 = new List<CPlayerInfo>();
                List<CPlayerInfo> team2 = new List<CPlayerInfo>();

                // 各チームに振り分け
                foreach (CPlayerInfo player in shuffleList)
                {
                    if (player.TeamID == TEAM_ID_NEUTRAL) continue;

                    if (team1.Count <= team2.Count)
                    {
                        // チーム1へ移動
                        if (player.TeamID != TEAM_ID_1)
                        {
                            MovePlayer(player.SoldierName, TEAM_ID_1, 0, true);
                        }
                        team1.Add(player);
                    }
                    else
                    {
                        // チーム2へ移動
                        if (player.TeamID != TEAM_ID_2)
                        {
                            MovePlayer(player.SoldierName, TEAM_ID_2, 0, true);
                        }
                        team2.Add(player);
                    }
                }

                ConsoleWrite("(Shuffle Thread) Shuffle Result Team1=" + team1.Count + " / Team2=" + team2.Count);
                ConsoleWrite("(Shuffle Thread) Shuffle End Successfully !!");

                // ADMINメッセージ
                SendGlobalMessage("Team shuffle was completely successful.");
            }
            catch (Exception e)
            {
                ConsoleWrite("(Shuffle Thread) Shuffle FAILED !!!"); 
                ConsoleWrite(e.Message);
                ConsoleWrite(e.StackTrace);
            }
        }

        // Sort delegate
        /// <summary>
        /// ランダムにソート
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        private int SortByRandom(CPlayerInfo p1, CPlayerInfo p2)
        {
            if (p1 == p2)
            {
                return 0;
            }

            // ランダムソート
            return this.randomGenerator.Next(-1, 2);
        }

        /// <summary>
        /// プレイヤーJOIN時にバランシングする
        /// </summary>
        /// <param name="soldierName"></param>
        private void BalancingJoinPlayer(String soldierName)
        {
            new Thread(new ThreadStart(() =>
            {
                // サーバから最新のプレイヤーリストを取得
                List<CPlayerInfo> playerList = getServerPlayersListSync();
                if (playerList != null)
                {
                    // バランシング対象プレイヤー
                    CPlayerInfo playerInfo = playerList.Find((p) => p.SoldierName == soldierName);
                    if (playerInfo == null)
                    {
                        ConsoleWrite("BalancingJoinPlayer: Can't find PlayerInfo");
                        return;
                    }

                    // ニュートラルの人数
                    int neutralcount = playerList.FindAll((p) => p.TeamID == TEAM_ID_NEUTRAL).Count;
                    // チーム1の人数
                    int team1count = playerList.FindAll((p) => p.TeamID == TEAM_ID_1).Count;
                    // チーム2の人数
                    int team2count = playerList.FindAll((p) => p.TeamID == TEAM_ID_2).Count;

                    ConsoleWrite("BalancingJoinPlayer: Name=" + playerInfo.SoldierName + " / TeamId=" + playerInfo.TeamID + " / All=" + playerList.Count + " / Neutral=" + neutralcount + " / Team1=" + team1count + " / Team2=" + team2count);

                    /*
                     * 人数が少ないチームへJOINさせる
                     */
                    // チーム人数の差
                    int absTeamDifference = Math.Abs(team1count - team2count);
                    if (absTeamDifference == 1)
                    {
                        bool isJoinLargeTeam = (playerInfo.TeamID == TEAM_ID_1 && team1count > team2count) || (playerInfo.TeamID == TEAM_ID_2 && team1count < team2count);
                        if (!isJoinLargeTeam)
                        {
                            // 少ないチームにJOINした場合は、そのまま移動させない
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM" + playerInfo.TeamID + " (SMALL TEAM)");
                        }
                        else
                        {
                            // チームが同数だった場合、ランダムにどちらかへJOINさせる
                            int randomTeamId = this.randomGenerator.Next(TEAM_ID_1, TEAM_ID_2 + 1);
                            // 移動元と移動先が同じ場合チーム移動させない
                            if (playerInfo.TeamID != randomTeamId)
                            {
                                // 移動先が満員の場合、移動させない（司令官込みの人数なので厳密には満員ではない場合もある）
                                int teamMaxCount = this.serverInfo.ServerInfo.MaxPlayerCount / 2;
                                if ((randomTeamId == TEAM_ID_1 && team1count < teamMaxCount) ||
                                    (randomTeamId == TEAM_ID_2 && team2count < teamMaxCount))
                                {
                                    AdminForceMovePlayer(soldierName, randomTeamId, 0, true);
                                }
                            }
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM" + randomTeamId + " (RANDOM)");
                        }
                    }
                    else if (absTeamDifference >= 2)
                    {
                        // 人数差が2人以上の場合、チーム移動させる
                        if (team1count < team2count)
                        {
                            // チーム1へ強制JOIN
                            if (playerInfo.TeamID != TEAM_ID_1)
                            {
                                AdminForceMovePlayer(soldierName, TEAM_ID_1, 0, true);
                            }
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM1 (FORCE)");
                        }
                        else
                        {
                            // チーム2へ強制JOIN
                            if (playerInfo.TeamID != TEAM_ID_2)
                            {
                                AdminForceMovePlayer(soldierName, TEAM_ID_2, 0, true);
                            }
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM2 (FORCE)");
                        }
                    }
                }
            })).Start();
        }

        /// <summary>
        /// 最新のプレイヤーリストを取得する
        /// </summary>
        /// <returns></returns>
        public List<CPlayerInfo> getServerPlayersListSync()
        {
            ConsoleWrite("getServerPlayersListSync: ListPlayers REQUEST");

            // イベントハンドルを初期化
            this.playerListEventHandle.Reset();

            // コマンドを投げるとOnListPlayersイベントが呼ばれる
            ServerCommand("admin.listPlayers", "all");

            // 最新のプレイヤーリストを取得するまで待つ
            playerListEventHandle.WaitOne(5000);

            // 最新のプレイヤーリストを返す
            return this.serverInfo.PlayerInfoList;
        }

        /// <summary>
        /// 逆のチームIDを取得する
        /// </summary>
        /// <param name="teamId"></param>
        /// <returns></returns>
        private int getOpposingTeamId(int teamId)
        {
            return (teamId == TEAM_ID_NEUTRAL) ? teamId : (teamId == TEAM_ID_1) ? TEAM_ID_2 : TEAM_ID_1;
        }

        /// <summary>
        /// チーム移動を許可するか？
        /// </summary>
        /// <returns></returns>
        private bool IsAllowTeamChange(){
            // サーバ人数が設定値未満はOK または ラウンド終了～開始までの間はOK
            return (this.serverInfo.PlayerInfoList.Count < this.allowTeamChangePlayerCount || this.roundInfo.IsAllowTeamChange);
        }

        /// <summary>
        /// ADMIN SAY
        /// </summary>
        /// <param name="message"></param>
        private void SendGlobalMessage(String message)
        {
            string pluginName = "(" + GetPluginName() + ") ";
            //ServerCommand("admin.say", pluginName + Regex.Replace(message, @"\^[0-9a-zA-Z]", ""), "all");
            ServerCommand("admin.say", pluginName + message, "all");
        }

        /// <summary>
        /// プレイヤーをチーム移動する
        /// </summary>
        /// <param name="name"></param>
        /// <param name="TeamId"></param>
        /// <param name="SquadId"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        private bool MovePlayer(String name, int TeamId, int SquadId, bool force)
        {
            if (this.virtualMode)
            {
                // 実際にチーム移動はしない
                ConsoleWarn("not moving ^b" + name + "^n, ^bvirtual_mode^n is ^bon^n");
                return false;
            }

            // プレイヤーのチーム移動
            this.ServerCommand("admin.movePlayer", name, TeamId.ToString(), SquadId.ToString(), force.ToString().ToLower());
            return true;
        }

        /// <summary>
        /// アドミンによる強制移動
        /// </summary>
        /// <param name="name"></param>
        /// <param name="TeamId"></param>
        /// <param name="SquadId"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        private bool AdminForceMovePlayer(String soldierName, int teamId, int squadId, bool force)
        {
            // チーム移動する前にADMINの強制移動であることを示すフラグを保持する(※admin.movePlayerで移動させてもOnPlayerTeamChangeが呼ばれるので、再度チーム移動させないように制御するために保持)
            string adminMovePlayerKey = soldierName + teamId;

            ConsoleWrite("AddForceMovePlayer: key=" + adminMovePlayerKey + " / count=" + this.roundInfo.AdminMovePlayers.Count);

            // 強制チーム移動プレイヤーを管理（追加）
            this.roundInfo.AddForceMovePlayer(adminMovePlayerKey);

            // プレイヤーをチーム移動する
            return MovePlayer(soldierName, teamId, squadId, force);
        }

    } // end BasicPlugin

    public class Server
    {
        /// <summary>
        /// サーバ情報
        /// </summary>
        public CServerInfo ServerInfo { get; set; }

        /// <summary>
        /// プレイヤーリスト
        /// </summary>
        public List<CPlayerInfo> PlayerInfoList { get; set; }
    }

    public class Round
    {
        /// <summary>
        /// ラウンドの経過時間
        /// </summary>
        public Stopwatch Time { get; set; }

        /// <summary>
        /// 管理者が強制移動させたプレイヤー
        /// </summary>
        public HashSet<string> AdminMovePlayers { get; set; }

        /// <summary>
        /// 管理者の強制移動イベントハンドラ(現在未使用)
        /// </summary>
        public Dictionary<string, EventWaitHandle> AdminMovePlayerEventHandle { get; set; }

        /// <summary>
        /// ラウンド開始時にチームスワップがあるのでその間の猶予時間
        /// </summary>
        public int AllowTeamChangeTime { get; set; }

        /// <summary>
        /// チーム移動を許可するか？
        /// </summary>
        public bool IsAllowTeamChange
        {
            get
            {
                // チーム移動可能な時間。ラウンド開始時にチームスワップがあるから猶予時間が必要
                return Time.ElapsedMilliseconds < AllowTeamChangeTime * 1000;
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public Round()
        {
            Time = new Stopwatch();
            AdminMovePlayers = new HashSet<string>();
            AdminMovePlayerEventHandle = new Dictionary<string, EventWaitHandle>();
            AllowTeamChangeTime = 45; // デフォルト45sec
        }

        /// <summary>
        /// ラウンドの開始
        /// </summary>
        public void Start()
        {
            // ラウンドタイマー初期化
            Time.Reset();
            Time.Start();

            // 強制移動プレイヤー初期化
            AdminMovePlayers.Clear();
        }

        /// <summary>
        /// ラウンドの終了
        /// </summary>
        public void End()
        {
            // ラウンドタイマー停止
            Time.Stop();
            Time.Reset();
        }

        public void AddForceMovePlayer(string playerName)
        {
            if (!this.AdminMovePlayers.Contains(playerName))
            {
                this.AdminMovePlayers.Add(playerName);
            }
        }

        public void RemoveForceMovePlayer(string playerName)
        {
            if (this.AdminMovePlayers.Contains(playerName))
            {
                this.AdminMovePlayers.Remove(playerName);
            }
        }
    }

} // end namespace PRoConEvents



