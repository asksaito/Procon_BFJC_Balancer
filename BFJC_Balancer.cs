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

        #region �萔
        /// <summary>
        /// �`�[��ID�F�j���[�g����
        /// </summary>
        public const int TEAM_ID_NEUTRAL = 0;
        /// <summary>
        /// �`�[��ID�F�`�[���P
        /// </summary>
        public const int TEAM_ID_1 = 1;
        /// <summary>
        /// �`�[��ID�F�`�[���Q
        /// </summary>
        public const int TEAM_ID_2 = 2;
        #endregion

        #region �����o
        //private bool fIsEnabled;
        private int fDebugLevel;
        
        /// <summary>
        /// �o�[�`�������[�h
        /// </summary>
        private bool virtualMode;

        /// <summary>
        /// �V���b�t���f�B���C�^�C��
        /// </summary>
        private int shuffleDelayTime;

        /// <summary>
        /// �`�[���ړ���������v���C���[���i���ꖢ���̐l���̏ꍇ�A�`�[���ړ���������j
        /// </summary>
        public int allowTeamChangePlayerCount;
        
        /// <summary>
        /// ���������I�u�W�F�N�g
        /// </summary>
        private Random randomGenerator;

        /// <summary>
        /// �v���C���[���X�g�X�V�ʒm�C�x���g�n���h��
        /// </summary>
        private EventWaitHandle playerListEventHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        /// <summary>
        /// �T�[�o�̏��
        /// </summary>
        private Server serverInfo = new Server();

        /// <summary>
        /// ���E���h���
        /// </summary>
        private Round roundInfo = new Round();
        #endregion

        /// <summary>
        /// �R���X�g���N�^
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
            // ���E���h�J�n(�v���O�C���L����)
            this.roundInfo.Start();

            //fIsEnabled = true;
            ConsoleWrite("Enabled!");
        }

        public void OnPluginDisable()
        {
            // ���E���h�I��(�v���O�C��������)
            this.roundInfo.End();

            //fIsEnabled = false;
            ConsoleWrite("Disabled!");
        }
        
        public override void OnVersion(String serverType, String version) { }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            // �T�[�o���ێ�
            this.serverInfo.ServerInfo = serverInfo;

            //ConsoleWrite("OnServerInfo: Debug level = " + fDebugLevel);
        }

        public override void OnResponseError(List<String> requestWords, String error) { }

        /// <summary>
        /// �v���C���[���X�g��M�C�x���g
        /// </summary>
        /// <param name="players"></param>
        /// <param name="subset"></param>
        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            // �T�[�o�����M�����v���C���[���X�g��ۑ�
            this.serverInfo.PlayerInfoList = new List<CPlayerInfo>(players);

            // �����C�x���g�ʒm
            this.playerListEventHandle.Set();

            ConsoleWrite("OnListPlayer: PlayerCount = " + this.serverInfo.PlayerInfoList.Count.ToString());
        }

        /*
         * ��x�ɑ�ʂ�JOIN���Ă���ƁA�`�[���o�����X���΂�s�������̂ŁA��U��~
         * ���ƁA�i�ߊ���JOIN���Ă����ꍇ�̋������s��
         */
        public override void OnPlayerJoin(String soldierName)
        {
            //new Thread(new ThreadStart(() =>
            //{
            //    // �T�[�o����ŐV�̃v���C���[���X�g���擾
            //    List<CPlayerInfo> playerList = getServerPlayersListSync();
            //    if (playerList != null)
            //    {
            //        // �j���[�g�����̐l��
            //        int neutralcount = playerList.FindAll((p) => p.TeamID == TEAM_ID_NEUTRAL).Count;
            //        // �`�[��1�̐l��
            //        int team1count = playerList.FindAll((p) => p.TeamID == TEAM_ID_1).Count;
            //        // �`�[��2�̐l��
            //        int team2count = playerList.FindAll((p) => p.TeamID == TEAM_ID_2).Count;

            //        ConsoleWrite("OnPlayerJoin: Neutralcount = " + neutralcount + " / Team1Count = " + team1count + " / Team2Count = " + team2count);

            //        // �j���[�g�����̐l������A�\�z�����`�[���̐l�����v�Z
            //        for (int i = 0; i < neutralcount; i++)
            //        {
            //            if (team1count <= team2count) { team1count++; }
            //            else { team2count++; }
            //        }

            //        // �l�������Ȃ��`�[����JOIN������
            //        if (team1count == team2count)
            //        {
            //            // �`�[���������̏ꍇ�A�����_���ɂǂ��炩��JOIN������
            //            int randomTeamId = this.randomGenerator.Next(TEAM_ID_1, TEAM_ID_2 + 1);
            //            MovePlayer(soldierName, randomTeamId, 0, true);
            //            ConsoleWrite("=====> " + soldierName + " JOINING TEAM" + randomTeamId + " (RANDOM)");
            //        }
            //        else if (team1count < team2count)
            //        {
            //            // �`�[��1�֋���JOIN
            //            MovePlayer(soldierName, TEAM_ID_1, 0, true);
            //            ConsoleWrite("=====> " + soldierName + " JOINING TEAM1");
            //        }
            //        else
            //        {
            //            // �`�[��2�֋���JOIN
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
                // �A�h�~���ɂ��`�[���ړ��ł��邱�Ƃ�ʒm����
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

                // �A�h�~���ɂ��`�[���ړ� (���̃`�[���ɖ߂��Ȃ�)
                this.roundInfo.RemoveForceMovePlayer(movePlayerKey);

                ConsoleWrite("Don't move again [" + soldierName + "]. (AdminForceMove)");
            }
            else
            {
                // �v���C���[�ɂ��`�[���ړ�
                if (!IsAllowTeamChange()) // �`�[���ړ��\���H
                {
                    // �T�[�oJOIN���́A�����I�Ƀj���[�g��������`�[���ړ������̂ŁA���̎��͏��O����
                    CPlayerInfo player = this.serverInfo.PlayerInfoList.Find((p) => p.SoldierName == soldierName);
                    if (player == null || player.TeamID == TEAM_ID_NEUTRAL)
                    {
                        ConsoleWrite("Neutral to Team move by Server. (PlayerJoin)");

                        // �����`�[����JOIN������
                        BalancingJoinPlayer(soldierName);
                    }
                    else
                    {
                        // ���̃`�[���֖߂�
                        AdminForceMovePlayer(soldierName, getOpposingTeamId(teamId), 0, true);

                        ConsoleWrite("<===== [" + soldierName + "] has been moved by Admin automatically. (PlayerTeamSwitch)");
                        SendGlobalMessage(soldierName + " has been moved by Admin automatically. (TeamSwitch)");
                    }
                }
            }
        }

        /* 
         * �X���b�h�o�[�W����
         */ 
        //public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId)
        //{
        //    if (!IsAllowTeamChange()) // �`�[���ړ��s�H
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

        //            // OnPlayerMovedByAdmin�C�x���g�����Ȃ����҂��Ċm�F����
        //            int adminMoveEventWaitTime = 5000;// 5sec
        //            eventWaitHandle.WaitOne(adminMoveEventWaitTime);

        //            // 5�b�҂���OnPlayerMovedByAdmin�C�x���g�����Ȃ��ꍇ�A�v���C���[�ɂ��`�[���ړ��Ɣ��f����
        //            if (sw.ElapsedMilliseconds <= adminMoveEventWaitTime)
        //            {
        //                // �A�h�~���ɂ��`�[���ړ� (���̃`�[���ɖ߂��Ȃ�)
        //                ConsoleWrite("Don't move again player [" + soldierName + "]. (Admin force move back)");
        //            }
        //            else
        //            {
        //                // �v���C���[�ɂ��`�[���ړ� (���̃`�[���֖߂�)
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

            // ���E���h�̏I���ʒm
            this.roundInfo.End();

            // �`�[���̃V���b�t�����s
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

            // ���E���h�̊J�n�ʒm
            this.roundInfo.Start();
        } // BF3

        /// <summary>
        /// �`�[���V���b�t��
        /// </summary>
        private void Shuffle()
        {
            try
            {
                ConsoleWrite("(Shuffle Thread) Shuffle Start !! Waiting " + shuffleDelayTime + "sec ...");
                System.Threading.Thread.Sleep(this.shuffleDelayTime * 1000);// wait

                // �ŐV�̃v���C���[���X�g���T�[�o����擾
                List<CPlayerInfo> playerList = getServerPlayersListSync();
                if (playerList == null)
                {
                    ConsoleWrite("(Shuffle Thread) PlayerList is null !! Can't execute process.");
                    return;
                }
                ConsoleWrite("(Shuffle Thread) PlayerList Count=" + playerList.Count.ToString());

                // �����_���Ƀ\�[�g
                List<CPlayerInfo> shuffleList = new List<CPlayerInfo>(playerList);
                shuffleList.Sort(SortByRandom);

                List<CPlayerInfo> team1 = new List<CPlayerInfo>();
                List<CPlayerInfo> team2 = new List<CPlayerInfo>();

                // �e�`�[���ɐU�蕪��
                foreach (CPlayerInfo player in shuffleList)
                {
                    if (player.TeamID == TEAM_ID_NEUTRAL) continue;

                    if (team1.Count <= team2.Count)
                    {
                        // �`�[��1�ֈړ�
                        if (player.TeamID != TEAM_ID_1)
                        {
                            MovePlayer(player.SoldierName, TEAM_ID_1, 0, true);
                        }
                        team1.Add(player);
                    }
                    else
                    {
                        // �`�[��2�ֈړ�
                        if (player.TeamID != TEAM_ID_2)
                        {
                            MovePlayer(player.SoldierName, TEAM_ID_2, 0, true);
                        }
                        team2.Add(player);
                    }
                }

                ConsoleWrite("(Shuffle Thread) Shuffle Result Team1=" + team1.Count + " / Team2=" + team2.Count);
                ConsoleWrite("(Shuffle Thread) Shuffle End Successfully !!");

                // ADMIN���b�Z�[�W
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
        /// �����_���Ƀ\�[�g
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

            // �����_���\�[�g
            return this.randomGenerator.Next(-1, 2);
        }

        /// <summary>
        /// �v���C���[JOIN���Ƀo�����V���O����
        /// </summary>
        /// <param name="soldierName"></param>
        private void BalancingJoinPlayer(String soldierName)
        {
            new Thread(new ThreadStart(() =>
            {
                // �T�[�o����ŐV�̃v���C���[���X�g���擾
                List<CPlayerInfo> playerList = getServerPlayersListSync();
                if (playerList != null)
                {
                    // �o�����V���O�Ώۃv���C���[
                    CPlayerInfo playerInfo = playerList.Find((p) => p.SoldierName == soldierName);
                    if (playerInfo == null)
                    {
                        ConsoleWrite("BalancingJoinPlayer: Can't find PlayerInfo");
                        return;
                    }

                    // �j���[�g�����̐l��
                    int neutralcount = playerList.FindAll((p) => p.TeamID == TEAM_ID_NEUTRAL).Count;
                    // �`�[��1�̐l��
                    int team1count = playerList.FindAll((p) => p.TeamID == TEAM_ID_1).Count;
                    // �`�[��2�̐l��
                    int team2count = playerList.FindAll((p) => p.TeamID == TEAM_ID_2).Count;

                    ConsoleWrite("BalancingJoinPlayer: Name=" + playerInfo.SoldierName + " / TeamId=" + playerInfo.TeamID + " / All=" + playerList.Count + " / Neutral=" + neutralcount + " / Team1=" + team1count + " / Team2=" + team2count);

                    /*
                     * �l�������Ȃ��`�[����JOIN������
                     */
                    // �`�[���l���̍�
                    int absTeamDifference = Math.Abs(team1count - team2count);
                    if (absTeamDifference == 1)
                    {
                        bool isJoinLargeTeam = (playerInfo.TeamID == TEAM_ID_1 && team1count > team2count) || (playerInfo.TeamID == TEAM_ID_2 && team1count < team2count);
                        if (!isJoinLargeTeam)
                        {
                            // ���Ȃ��`�[����JOIN�����ꍇ�́A���̂܂܈ړ������Ȃ�
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM" + playerInfo.TeamID + " (SMALL TEAM)");
                        }
                        else
                        {
                            // �`�[���������������ꍇ�A�����_���ɂǂ��炩��JOIN������
                            int randomTeamId = this.randomGenerator.Next(TEAM_ID_1, TEAM_ID_2 + 1);
                            // �ړ����ƈړ��悪�����ꍇ�`�[���ړ������Ȃ�
                            if (playerInfo.TeamID != randomTeamId)
                            {
                                // �ړ��悪�����̏ꍇ�A�ړ������Ȃ��i�i�ߊ����݂̐l���Ȃ̂Ō����ɂ͖����ł͂Ȃ��ꍇ������j
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
                        // �l������2�l�ȏ�̏ꍇ�A�`�[���ړ�������
                        if (team1count < team2count)
                        {
                            // �`�[��1�֋���JOIN
                            if (playerInfo.TeamID != TEAM_ID_1)
                            {
                                AdminForceMovePlayer(soldierName, TEAM_ID_1, 0, true);
                            }
                            ConsoleWrite("=====> " + soldierName + " JOINING TEAM1 (FORCE)");
                        }
                        else
                        {
                            // �`�[��2�֋���JOIN
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
        /// �ŐV�̃v���C���[���X�g���擾����
        /// </summary>
        /// <returns></returns>
        public List<CPlayerInfo> getServerPlayersListSync()
        {
            ConsoleWrite("getServerPlayersListSync: ListPlayers REQUEST");

            // �C�x���g�n���h����������
            this.playerListEventHandle.Reset();

            // �R�}���h�𓊂����OnListPlayers�C�x���g���Ă΂��
            ServerCommand("admin.listPlayers", "all");

            // �ŐV�̃v���C���[���X�g���擾����܂ő҂�
            playerListEventHandle.WaitOne(5000);

            // �ŐV�̃v���C���[���X�g��Ԃ�
            return this.serverInfo.PlayerInfoList;
        }

        /// <summary>
        /// �t�̃`�[��ID���擾����
        /// </summary>
        /// <param name="teamId"></param>
        /// <returns></returns>
        private int getOpposingTeamId(int teamId)
        {
            return (teamId == TEAM_ID_NEUTRAL) ? teamId : (teamId == TEAM_ID_1) ? TEAM_ID_2 : TEAM_ID_1;
        }

        /// <summary>
        /// �`�[���ړ��������邩�H
        /// </summary>
        /// <returns></returns>
        private bool IsAllowTeamChange(){
            // �T�[�o�l�����ݒ�l������OK �܂��� ���E���h�I���`�J�n�܂ł̊Ԃ�OK
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
        /// �v���C���[���`�[���ړ�����
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
                // ���ۂɃ`�[���ړ��͂��Ȃ�
                ConsoleWarn("not moving ^b" + name + "^n, ^bvirtual_mode^n is ^bon^n");
                return false;
            }

            // �v���C���[�̃`�[���ړ�
            this.ServerCommand("admin.movePlayer", name, TeamId.ToString(), SquadId.ToString(), force.ToString().ToLower());
            return true;
        }

        /// <summary>
        /// �A�h�~���ɂ�鋭���ړ�
        /// </summary>
        /// <param name="name"></param>
        /// <param name="TeamId"></param>
        /// <param name="SquadId"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        private bool AdminForceMovePlayer(String soldierName, int teamId, int squadId, bool force)
        {
            // �`�[���ړ�����O��ADMIN�̋����ړ��ł��邱�Ƃ������t���O��ێ�����(��admin.movePlayer�ňړ������Ă�OnPlayerTeamChange���Ă΂��̂ŁA�ēx�`�[���ړ������Ȃ��悤�ɐ��䂷�邽�߂ɕێ�)
            string adminMovePlayerKey = soldierName + teamId;

            ConsoleWrite("AddForceMovePlayer: key=" + adminMovePlayerKey + " / count=" + this.roundInfo.AdminMovePlayers.Count);

            // �����`�[���ړ��v���C���[���Ǘ��i�ǉ��j
            this.roundInfo.AddForceMovePlayer(adminMovePlayerKey);

            // �v���C���[���`�[���ړ�����
            return MovePlayer(soldierName, teamId, squadId, force);
        }

    } // end BasicPlugin

    public class Server
    {
        /// <summary>
        /// �T�[�o���
        /// </summary>
        public CServerInfo ServerInfo { get; set; }

        /// <summary>
        /// �v���C���[���X�g
        /// </summary>
        public List<CPlayerInfo> PlayerInfoList { get; set; }
    }

    public class Round
    {
        /// <summary>
        /// ���E���h�̌o�ߎ���
        /// </summary>
        public Stopwatch Time { get; set; }

        /// <summary>
        /// �Ǘ��҂������ړ��������v���C���[
        /// </summary>
        public HashSet<string> AdminMovePlayers { get; set; }

        /// <summary>
        /// �Ǘ��҂̋����ړ��C�x���g�n���h��(���ݖ��g�p)
        /// </summary>
        public Dictionary<string, EventWaitHandle> AdminMovePlayerEventHandle { get; set; }

        /// <summary>
        /// ���E���h�J�n���Ƀ`�[���X���b�v������̂ł��̊Ԃ̗P�\����
        /// </summary>
        public int AllowTeamChangeTime { get; set; }

        /// <summary>
        /// �`�[���ړ��������邩�H
        /// </summary>
        public bool IsAllowTeamChange
        {
            get
            {
                // �`�[���ړ��\�Ȏ��ԁB���E���h�J�n���Ƀ`�[���X���b�v�����邩��P�\���Ԃ��K�v
                return Time.ElapsedMilliseconds < AllowTeamChangeTime * 1000;
            }
        }

        /// <summary>
        /// �R���X�g���N�^
        /// </summary>
        public Round()
        {
            Time = new Stopwatch();
            AdminMovePlayers = new HashSet<string>();
            AdminMovePlayerEventHandle = new Dictionary<string, EventWaitHandle>();
            AllowTeamChangeTime = 45; // �f�t�H���g45sec
        }

        /// <summary>
        /// ���E���h�̊J�n
        /// </summary>
        public void Start()
        {
            // ���E���h�^�C�}�[������
            Time.Reset();
            Time.Start();

            // �����ړ��v���C���[������
            AdminMovePlayers.Clear();
        }

        /// <summary>
        /// ���E���h�̏I��
        /// </summary>
        public void End()
        {
            // ���E���h�^�C�}�[��~
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



