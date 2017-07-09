using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using NetworkCommsDotNet;
using System.Xml;
using System.Xml.Serialization;
using ProtoBuf;
using System.Timers;
using System.Windows.Threading;
using System.Net;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Potlatch_Judger
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
#if DEBUG
		public static bool bSoundEnabled = false;
		public static bool bAnyFinished = true;
        public static bool bOnlyConnectedFinished = true;
		public static double RoutineMinutesLength = .1;
#else
        public static bool bSoundEnabled = true;
		public static bool bAnyFinished = false;
        public static bool bOnlyConnectedFinished = true;
		public static double RoutineMinutesLength = 3;
#endif
		public static MainWindow SelfObj;
		public HatSorter HatSorterWindow = new HatSorter();
		public static double DiffPowerBase = 1.65;
		public static float DiffPointsMulti = .1f;
		public static float MusicPointsMulti = 80f;
		public static float AiPointsPerCheck = 100f;
		Button CurEditingBut = null;
		public int MaxCounterScore = 99;
		string SaveFolderPath = "";
		public static bool bRoutineRecording = false;
		public static bool bWaitingForReady = true;
		DateTime StartTime = new DateTime();
		bool bEnteringRoutineNumber = false;
        int CurrentEnteringRoutineNumber = 0;
		DiffScoreData CurDiffScore = new DiffScoreData();
		List<DiffScoreData> DiffScoreResults = new List<DiffScoreData>();
		bool bIsDiffMouse = false;
		//bool bIsDiffTouch = false;
		Point MousePos;
		public static bool bIsClientConnected
		{
			get
			{
				bool bConnectedToServer = false;
				if (!PingServerTimer.Enabled)
				{
					foreach (ConnectionInfo ci in NetworkComms.AllConnectionInfo())
					{
						if (ci.ConnectionType == ConnectionType.TCP)
						{
							bConnectedToServer = true;
							break;
						}
					}
				}

				return bConnectedToServer;
			}
		}
		string _displayDiffScore = "None";
		public string DisplayDiffScore { get { return _displayDiffScore; } set { _displayDiffScore = value; NotifyPropertyChanged("DisplayDiffScore"); } }
		Timer TickTimer = new Timer();
		public event PropertyChangedEventHandler PropertyChanged;
		public static Timer EndRoutineTimer = new Timer();
		public static DateTime RoutineStartTime;
		public double SecondsSinceRoutineStart { get { return bRoutineRecording ? (DateTime.Now - RoutineStartTime).TotalSeconds : TotalRoutineSeconds; } }
		public double SecondsIntoRoutine { get { return bRoutineRecording ? Math.Min(TotalRoutineSeconds, (DateTime.Now - RoutineStartTime).TotalSeconds) : TotalRoutineSeconds; } }
		public double TotalRoutineSeconds { get { return (int)(RoutineMinutesLength * 60); } }
		public static string[] AiNames = { "Caught First Throw", "Caught Last Catch", "Caught Clock Coop", "Hit a Music Cue", "Caught UD Coop",
											 "Caught Counter Coop", "Throw without re-grip", "1 Move with Pointed Toe" };
		public double LastMusicScore = -1;
		public float MusicScoreCooldown = -1;
		public static bool bWaitingResults = false;
		public static int FinishedJudgerFlags = 0;
		public static bool bInitScores = false;
		public static bool bScoreboardRoutineStart = false;
		public static bool bResultsDirty = false;
		int _CurrentRoutineIndex = -1;
		public int CurrentRoutineIndex
		{
			get { return _CurrentRoutineIndex; }
			set
			{
				_CurrentRoutineIndex = value;
                NotifyPropertyChanged("RoutineButtonText");
				UpdateTeamInfo();
			}
		}
		public string RoutineButtonText
		{
			get
			{
				string ButStr = "Routine Button Text";

				if (CurrentRoutineIndex >= 0 && CurrentRoutineIndex < PoolScores.AllRoutineScores.Count)
					ButStr = (CurrentRoutineIndex + 1) + ". " + PoolScores.AllRoutineScores[CurrentRoutineIndex].TeamName;
				else
					ButStr = "Enter New Routine Number";

				return ButStr;
			}
		}
		string _JudgeRoutineText = "";
		public string JudgeRoutineText { get { return _JudgeRoutineText; } set { _JudgeRoutineText = value; NotifyPropertyChanged("JudgeRoutineText"); } }
		public static bool bIsScoreboard = false;
		Pen DiffLinePen = new Pen();
		Pen LeaderTwoPointScorePen = new Pen();
		Pen AdjustedDiffLinePen = new Pen();
		Pen DiffGraphBgPen = new Pen();
		Pen MovePen = new Pen();
		Pen CatchPen = new Pen();
		Pen BobblePen = new Pen();
		Pen DropPen = new Pen();
		Pen DiffGraphScorePen = new Pen();
		RenderTargetBitmap DiffGraphBitmap;
		RenderTargetBitmap WheelBitmap;
		DrawingVisual DiffGraphVisual = new DrawingVisual();
		DrawingVisual WheelVisual = new DrawingVisual();
		public static double DiffWindowBefore = 1.1;
		public static double DiffWindowAfter = 2.2;
		double SplitIntervalTime = 5.0;
		int SplitInterval = -1;
		public static Dictionary<ConnectionInfo, string> ConnectionInfoToJudgeIdMap = new Dictionary<ConnectionInfo, string>();
		public static List<ConnectionInfo> RecentConnectedClients = new List<ConnectionInfo>();
		public string TimeRemainingText
		{
			get
			{
				double RemainingSeconds = RoutineMinutesLength * 60;
				if (bRoutineRecording)
				{
					RemainingSeconds = Math.Max(0.0, RoutineMinutesLength * 60 - SecondsIntoRoutine);
				}

				int Minutes = (int)(RemainingSeconds / 60);
				int Seconds = ((int)Math.Ceiling(RemainingSeconds)) % 60;

				return Minutes + ":" + Seconds.ToString("00");
			}
		}
		ObservableCollection<RankingVisual> _ScorboardRankings = new ObservableCollection<RankingVisual>();
		public ObservableCollection<RankingVisual> ScorboardRankings { get { return _ScorboardRankings; } set { _ScorboardRankings = value; NotifyPropertyChanged("ScorboardRankings"); } }
		ObservableCollection<string> _ScorboardToPlay = new ObservableCollection<string>();
		public ObservableCollection<string> ScorboardToPlay { get { return _ScorboardToPlay; } set { _ScorboardToPlay = value; NotifyPropertyChanged("ScorboardToPlay"); } }
		bool bShowingRankingScoreboard = false;
		bool bShowingScoreboardRandom = false;
		BitmapSource DiffGraphBg = null;
		MediaPlayer[] DiffMediaPlayers = new MediaPlayer[10];
		MediaPlayer EndMediaPlayer = new MediaPlayer();
		//int LastDiffScore = -1;
		public ObservableCollection<BackupDisplay> _BackupDisplayList = new ObservableCollection<BackupDisplay>();
		public ObservableCollection<BackupDisplay> BackupDisplayList { get { return _BackupDisplayList; } set { _BackupDisplayList = value; NotifyPropertyChanged("BackupDisplayList"); } }
		public List<string> WheelNames = new List<string>();
		public List<string> RandomNames = new List<string>();
		public double WheelRotation = 0;
		public double TargetWheelDistance = 0;
		public bool bHatTeamAltBool = false;
		public int CurHatTeamMemberCount = 0;
		public int WheelEndIndex = -1;
		public int CurTeamNumber = 1;
		public static System.Threading.Semaphore WheelDataSema = new System.Threading.Semaphore(1, 1);
		public int SpinIndex = 0;
		Timer NextSpinTimer = new Timer();
		Brush judge1Bg = Brushes.White;
		public Brush Judge1Bg { get { return judge1Bg; } set { judge1Bg = value; NotifyPropertyChanged("Judge1Bg"); } }
		Brush judge2Bg = Brushes.White;
		public Brush Judge2Bg { get { return judge2Bg; } set { judge2Bg = value; NotifyPropertyChanged("Judge2Bg"); } }
		Brush judge3Bg = Brushes.White;
		public Brush Judge3Bg { get { return judge3Bg; } set { judge3Bg = value; NotifyPropertyChanged("Judge3Bg"); } }

		#region Scoreboard Stuff
		float MaxDiffHeight = 10f;
		public string ScoreboardTimeText { get { return "Remaining Time: " + TimeRemainingText; } }
		float _LeaderDiff = 0;
		float _LeaderAi = 0;
		float _LeaderMusic = 0;
		float LeaderDiff { get { return _LeaderDiff > 0 ? _LeaderDiff : CurrentDiff; } set { _LeaderDiff = value; NotifyPropertyChanged("LeaderDiffText"); NotifyPropertyChanged("LeaderTotalText"); } }
		float LeaderAi { get { return _LeaderAi > 0 ? _LeaderAi : CurrentAi; } set { _LeaderAi = value; NotifyPropertyChanged("LeaderAiText"); NotifyPropertyChanged("LeaderTotalText"); } }
		float LeaderMusic { get { return _LeaderMusic > 0 ? _LeaderMusic : CurrentMusic; } set { _LeaderMusic = value; NotifyPropertyChanged("LeaderMusicText"); NotifyPropertyChanged("LeaderTotalText"); } }
		float LeaderTotal { get { return LeaderDiff + LeaderAi + LeaderMusic; } }
		float _LeaderSplitDiff = 0;
		float _LeaderSplitAi = 0;
		float _LeaderSplitMusic = 0;
		float LeaderSplitDiff { get { return _LeaderSplitDiff >= 0 ? _LeaderSplitDiff : CurrentDiff; } set { _LeaderSplitDiff = value; NotifyPropertyChanged("LeaderSplitDiffText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float LeaderSplitAi { get { return _LeaderSplitAi >= 0 ? _LeaderSplitAi : CurrentAi; } set { _LeaderSplitAi = value; NotifyPropertyChanged("LeaderSplitAiText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaAiText"); NotifyPropertyChanged("DeltaAiBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float LeaderSplitMusic { get { return _LeaderSplitMusic >= 0 ? _LeaderSplitMusic : CurrentMusic; } set { _LeaderSplitMusic = value; NotifyPropertyChanged("LeaderSplitMusicText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaMusicText"); NotifyPropertyChanged("DeltaMusicBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		
		float _CurrentDiff = 0;
		float _CurrentAi = 0;
		float _CurrentMusic = 0;
		float CurrentDiff { get { return _CurrentDiff; } set { _CurrentDiff = value; NotifyPropertyChanged("CurrentDiffText"); NotifyPropertyChanged("CurrentTotalText"); } }
		float CurrentAi { get { return _CurrentAi; } set { _CurrentAi = value; NotifyPropertyChanged("CurrentAiText"); NotifyPropertyChanged("CurrentTotalText"); } }
		float CurrentMusic { get { return _CurrentMusic; } set { _CurrentMusic = value; NotifyPropertyChanged("CurrentMusicText"); NotifyPropertyChanged("CurrentTotalText"); } }
		float CurrentTotal { get { return CurrentDiff + CurrentAi + CurrentMusic; } }

		float CurrentSplitDiff { get; set; }
		float CurrentSplitAi { get; set; }
		float CurrentSplitMusic { get; set; }
		SplitData currentData = new SplitData();
		SplitData currentSplit = new SplitData();
		SplitData leaderSplit = new SplitData();

		float CurrentSplitJudge1 { get { return currentSplit.Judge1Score > 0 ? currentSplit.Judge1Score : currentData.Judge1Score; } set { currentSplit.Judge1Score = value; NotifyPropertyChanged("CurrentSplitDiffText"); NotifyPropertyChanged("CurrentSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float CurrentSplitJudge2 { get { return currentSplit.Judge2Score > 0 ? currentSplit.Judge2Score : currentData.Judge1Score; } set { currentSplit.Judge2Score = value; NotifyPropertyChanged("CurrentSplitDiffText"); NotifyPropertyChanged("CurrentSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float CurrentSplitJudge3 { get { return currentSplit.Judge3Score > 0 ? currentSplit.Judge3Score : currentData.Judge1Score; } set { currentSplit.Judge3Score = value; NotifyPropertyChanged("CurrentSplitDiffText"); NotifyPropertyChanged("CurrentSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float CurrentSplitTotal { get { return currentSplit.TotalScore; } }

		float LeaderSplitJudge1 { get { return leaderSplit.Judge1Score >= 0 ? leaderSplit.Judge1Score : currentData.Judge1Score; } set { leaderSplit.Judge1Score = value; NotifyPropertyChanged("LeaderSplitDiffText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float LeaderSplitJudge2 { get { return leaderSplit.Judge2Score >= 0 ? leaderSplit.Judge2Score : currentData.Judge2Score; } set { leaderSplit.Judge2Score = value; NotifyPropertyChanged("LeaderSplitDiffText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }
		float LeaderSplitJudge3 { get { return leaderSplit.Judge3Score >= 0 ? leaderSplit.Judge3Score : currentData.Judge3Score; } set { leaderSplit.Judge3Score = value; NotifyPropertyChanged("LeaderSplitDiffText"); NotifyPropertyChanged("LeaderSplitTotalText"); NotifyPropertyChanged("DeltaDiffText"); NotifyPropertyChanged("DeltaDiffBrush"); NotifyPropertyChanged("DeltaTotalText"); NotifyPropertyChanged("DeltaTotalBrush"); } }

		float LeaderSplitTotal { get { return LeaderSplitJudge1 + LeaderSplitJudge2 + LeaderSplitJudge3; } }

		float judge1Multiplier = 0f;
		float judge2Multiplier = 0f;
		float judge3Multiplier = 0f;
		float Judge1Multiplier { get { return judge1Multiplier; } set { judge1Multiplier = value; NotifyPropertyChanged("Judge1MultiplierDisplay"); } }
		float Judge2Multiplier { get { return judge2Multiplier; } set { judge2Multiplier = value; NotifyPropertyChanged("Judge2MultiplierDisplay"); } }
		float Judge3Multiplier { get { return judge3Multiplier; } set { judge3Multiplier = value; NotifyPropertyChanged("Judge3MultiplierDisplay"); } }
		public string Judge1MultiplierDisplay { get { return Judge1Multiplier.ToString("0.0"); } }
		public string Judge2MultiplierDisplay { get { return Judge2Multiplier.ToString("0.0"); } }
		public string Judge3MultiplierDisplay { get { return Judge3Multiplier.ToString("0.0"); } }

		float judge1Score = 0f;
		float judge2Score = 0f;
		float judge3Score = 0f;
		float Judge1Score { get { return judge1Score; } set { judge1Score = value; NotifyPropertyChanged("Judge1ScoreDisplay"); NotifyPropertyChanged("TotalScoreDisplay"); } }
		float Judge2Score { get { return judge2Score; } set { judge2Score = value; NotifyPropertyChanged("Judge2ScoreDisplay"); NotifyPropertyChanged("TotalScoreDisplay"); } }
		float Judge3Score { get { return judge3Score; } set { judge3Score = value; NotifyPropertyChanged("Judge3ScoreDisplay"); NotifyPropertyChanged("TotalScoreDisplay"); } }
		public string Judge1ScoreDisplay { get { return Judge1Score.ToString("0.0"); } }
		public string Judge2ScoreDisplay { get { return Judge2Score.ToString("0.0"); } }
		public string Judge3ScoreDisplay { get { return Judge3Score.ToString("0.0"); } }
		public string TotalScoreDisplay { get { return (Judge1Score + Judge2Score + Judge3Score).ToString("0.0"); } }

		float judge1LeaderScore = 0f;
		float judge2LeaderScore = 0f;
		float judge3LeaderScore = 0f;
		float Judge1LeaderScore { get { return judge1LeaderScore; } set { judge1LeaderScore = value; NotifyPropertyChanged("Judge1LeaderScoreDisplay"); NotifyPropertyChanged("TotalLeaderScoreDisplay"); } }
		float Judge2LeaderScore { get { return judge2LeaderScore; } set { judge2LeaderScore = value; NotifyPropertyChanged("Judge2LeaderScoreDisplay"); NotifyPropertyChanged("TotalLeaderScoreDisplay"); } }
		float Judge3LeaderScore { get { return judge3LeaderScore; } set { judge3LeaderScore = value; NotifyPropertyChanged("Judge3LeaderScoreDisplay"); NotifyPropertyChanged("TotalLeaderScoreDisplay"); } }
		public string Judge1LeaderScoreDisplay { get { return Judge1LeaderScore.ToString("0.0"); } }
		public string Judge2LeaderScoreDisplay { get { return Judge2LeaderScore.ToString("0.0"); } }
		public string Judge3LeaderScoreDisplay { get { return Judge3LeaderScore.ToString("0.0"); } }
		public string TotalLeaderScoreDisplay { get { return (Judge1LeaderScore + Judge2LeaderScore + Judge3LeaderScore).ToString("0.0"); } }

		string _LeaderTeamName = null;
		public string LeaderTeamText
		{
			get
			{
				string Ret = "Leader: ";
				if (_LeaderTeamName != null)
					Ret += _LeaderTeamName;
				else if (_CurrentTeamName != null)
					Ret += _CurrentTeamName;
				else
					Ret = "Need Pool Data";

				return Ret;
			}
			set { _LeaderTeamName = value; NotifyPropertyChanged("LeaderTeamText"); }
		}
		public string LeaderDiffText { get { return LeaderDiff.ToString("0.0"); } }
		public string LeaderAiText { get { return LeaderAi.ToString("0.0"); } }
		public string LeaderMusicText { get { return LeaderMusic.ToString("0.0"); } }
		public string LeaderTotalText { get { return LeaderTotal.ToString("0.0"); } }
		public string LeaderSplitDiffText { get { return LeaderSplitDiff.ToString("0.0"); } }
		public string LeaderSplitAiText { get { return LeaderSplitAi.ToString("0.0"); } }
		public string LeaderSplitMusicText { get { return LeaderSplitMusic.ToString("0.0"); } }
		public string LeaderSplitTotalText { get { return LeaderSplitTotal.ToString("0.0"); } }
		string _CurrentTeamName = null;
		public string CurrentTeamText
		{
			get
			{
				string Ret = "Playing: ";
				if (_CurrentTeamName != null)
					Ret += _CurrentTeamName;
				else
					Ret = "Need Pool Data";

				return Ret;
			}
			set { _CurrentTeamName = value; NotifyPropertyChanged("CurrentTeamText"); NotifyPropertyChanged("LeaderTeamText"); }
		}
		string NextTeamName = null;
		public string CurrentDiffText { get { return CurrentDiff.ToString("0.0"); } }
		public string CurrentAiText { get { return CurrentAi.ToString("0.0"); } }
		public string CurrentMusicText { get { return CurrentMusic.ToString("0.0"); } }
		public string CurrentTotalText { get { return CurrentTotal.ToString("0.0"); } }
		public string CurrentSplitDiffText { get { return CurrentSplitDiff.ToString("0.0"); } }
		public string CurrentSplitAiText { get { return CurrentSplitAi.ToString("0.0"); } }
		public string CurrentSplitMusicText { get { return CurrentSplitMusic.ToString("0.0"); } }
		public string CurrentSplitTotalText { get { return CurrentSplitTotal.ToString("0.0"); } }
		public string DeltaDiffText
		{
			get 
			{
				float Delta = LeaderSplitDiff - CurrentSplitDiff;
				if (Math.Abs(Delta) < .1f)
					return "0.0";
				else if (Delta > 0)
					return "-" + Delta.ToString("0.0");
				else
					return Delta.ToString("0.0").Replace("-", "+");
			}
		}
		public string DeltaAiText
		{
			get
			{
				float Delta = LeaderSplitAi - CurrentSplitAi;
				if (Math.Abs(Delta) < .1f)
					return "0.0";
				else if (Delta > 0)
					return "-" + Delta.ToString("0.0");
				else
					return Delta.ToString("0.0").Replace("-", "+");
			}
		}
		public string DeltaMusicText
		{
			get
			{
				float Delta = LeaderSplitMusic - CurrentSplitMusic;
				if (Math.Abs(Delta) < .1f)
					return "0.0";
				else if (Delta > 0)
					return "-" + Delta.ToString("0.0");
				else
					return Delta.ToString("0.0").Replace("-", "+");
			}
		}
		public string DeltaTotalText
		{
			get
			{
				float Delta = LeaderSplitTotal - CurrentSplitTotal;
				if (Math.Abs(Delta) < .1f)
					return "0.0";
				else if (Delta > 0)
					return "-" + Delta.ToString("0.0");
				else
					return Delta.ToString("0.0").Replace("-", "+");
			}
		}
		public Brush DeltaDiffBrush
		{
			get
			{
				if (Math.Abs(LeaderSplitDiff - CurrentSplitDiff) < .1f)
					return Brushes.Black;
				else if (LeaderSplitDiff > CurrentSplitDiff)
					return Brushes.Red;
				else
					return Brushes.Green;
			}
		}
		public Brush DeltaAiBrush
		{
			get
			{
				if (Math.Abs(LeaderSplitAi - CurrentSplitAi) < .1f)
					return Brushes.Black;
				else if (LeaderSplitAi > CurrentSplitAi)
					return Brushes.Red;
				else
					return Brushes.Green;
			}
		}
		public Brush DeltaMusicBrush
		{
			get
			{
				if (Math.Abs(LeaderSplitMusic - CurrentSplitMusic) < .1f)
					return Brushes.Black;
				else if (LeaderSplitMusic > CurrentSplitMusic)
					return Brushes.Red;
				else
					return Brushes.Green;
			}
		}
		public Brush DeltaTotalBrush
		{
			get
			{
				if (Math.Abs(LeaderSplitTotal - CurrentSplitTotal) < .1f)
					return Brushes.Black;
				else if (LeaderSplitTotal > CurrentSplitTotal)
					return Brushes.Red;
				else
					return Brushes.Green;
			}
		}
		#endregion

		// Judge data
		JudgeCountData CurJudgeCounterData = new JudgeCountData();
		JudgeDiffData CurJudgeDiffData = new JudgeDiffData();
		JudgeAiData CurJudgeAiData = new JudgeAiData();
		JudgeMusicData CurJudgeMusicData = new JudgeMusicData();
		JudgeBackupData BackupData = new JudgeBackupData();
		public static TwoPointBackupData TwoPointBackupServerData = null;

		// Results
		public static ComboScore CurComboScore = new ComboScore();
		public static List<ComboScore> CurRoutineComboScores = new List<ComboScore>();
		public static bool[] AiScores = Enumerable.Repeat(false, 8).ToArray();
		public static PoolData PoolScores = new PoolData();
		public static RoutineScore LeaderScore = null;
		public static List<SplitData> CurSplits = new List<SplitData>();
		public static SplitData CurSplit = new SplitData();
		public static List<RoutineScore> SortedResults = new List<RoutineScore>();

		// Networking
		public static string ServerIpAddress = "127.0.0.1";
		string _NetStatusStr = "Disconnected";
		public string NetStatusStr { get { return _NetStatusStr; } set { _NetStatusStr = value; NotifyPropertyChanged("NetStatusStr"); } }
		SolidColorBrush _NetStatusColor = Brushes.LightSalmon;
		public SolidColorBrush NetStatusColor { get { return _NetStatusColor; } set { _NetStatusColor = value; NotifyPropertyChanged("NetStatusColor"); } }
		public SolidColorBrush SavedNetStatusColor;
		public static double NetExceptionTimer = 0;
		public static List<CounterData> RecievedCounterData = new List<CounterData>();
		public static Dictionary<string, List<DiffNetData>> RecievedDiffData = new Dictionary<string, List<DiffNetData>>();
		public static Dictionary<string, List<DiffNetData>> ScoreboardLeaderRecievedDiffData = new Dictionary<string, List<DiffNetData>>();
		public static Dictionary<string, int> RecievedDiffDataLastSentToScoreboard = new Dictionary<string, int>();
		public static List<MoveNetData> RecievedMoveData = new List<MoveNetData>();
		public static List<string> RecievedAiData = new List<string>();
		public static float RecievedMusicScore = -1;
		public static string RecievedTeamInfo = "Waiting to connect";
		public static LeaderNetData RecievedLeaderData = null;
		public static float RecievedCurrentDiffScore = -1;
		public static float RecievedCurrentAiScore = -1;
		public static SplitNetData RecievedSplitData = null;
		public static int RecievedToggleScoreboard = -1;
		public static int RecievedToggleScoreboardRandom = -1;
		public static List<JudgeCountData> RecievedBackupCount = new List<JudgeCountData>();
		public static List<string> RecievedConfirmBackupCount = new List<string>();
		public static List<JudgeDiffData> RecievedBackupDiff = new List<JudgeDiffData>();
		public static List<string> RecievedConfirmBackupDiff = new List<string>();
		public static List<JudgeAiData> RecievedBackupAi = new List<JudgeAiData>();
		public static List<string> RecievedConfirmBackupAi = new List<string>();
		public static List<JudgeMusicData> RecievedBackupMusic = new List<JudgeMusicData>();
		public static List<string> RecievedConfirmBackupMusic = new List<string>();
		public static Timer PingServerTimer = new Timer();
		public static ConnectionInfo ServerConnectionInfo = null;
		public static List<ScoreboardConnectionInfo> ScoreboardConnections = new List<ScoreboardConnectionInfo>();
		public float DiffSendCooldown = 0;
		public static bool bInitRankings = false;
		public static List<RankingVisual> RecievedRankingVisuals = new List<RankingVisual>();
		public static List<string> RecievedWaitingTeams = new List<string>();
		public static HatTeamNetData RecievedHatNames = null;
		public static bool bRecievedStartWheelSpin = false;
		public static System.Threading.Semaphore RecentConnectedListSema = new System.Threading.Semaphore(1, 1);
		public static System.Threading.Semaphore ResultsSema = new System.Threading.Semaphore(1, 1);
		public static float SendBackupCooldownTimer = 0;

		public MainWindow()
		{
			InitializeComponent();

			JudgersTabControl.SelectedIndex = 0;

			Touch.FrameReported += new TouchFrameEventHandler(Touch_FrameReported);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			float[] diffs = { 10, 10 };
			float total = 0;
			foreach (float f in diffs)
				total += (float)Math.Pow(DiffPowerBase, f);
			total *= diffs.Length * DiffPointsMulti;

			SelfObj = this;

			CounterText.Text = "0";

			SaveFolderPath = Properties.Settings.Default.ResultsFolderPath;

			if (!Directory.Exists(SaveFolderPath))
			{
				DirectoryInfo CurDirInfo = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
				SaveFolderPath = CurDirInfo.Parent.Parent.Parent.FullName;
			}

			if (!Directory.Exists(SaveFolderPath))
				SaveFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

			JudgersTabControl.SelectedIndex = 0;

			TickTimer.Interval = 150;
			TickTimer.AutoReset = true;

			NextSpinTimer.Interval = 1000;
			NextSpinTimer.Elapsed += new ElapsedEventHandler(NextSpin_Elapsed);
			NextSpinTimer.AutoReset = false;

			DiffLinePen.Thickness = 18;
			DiffLinePen.Brush = Brushes.DimGray;
			DiffLinePen.StartLineCap = PenLineCap.Round;
			DiffLinePen.EndLineCap = PenLineCap.Round;

			AdjustedDiffLinePen.Thickness = 10;
			AdjustedDiffLinePen.Brush = Brushes.Black;
			AdjustedDiffLinePen.StartLineCap = PenLineCap.Round;
			AdjustedDiffLinePen.EndLineCap = PenLineCap.Round;

			LeaderTwoPointScorePen.Thickness = 18;
			LeaderTwoPointScorePen.Brush = Brushes.Peru;
			LeaderTwoPointScorePen.StartLineCap = PenLineCap.Round;
			LeaderTwoPointScorePen.EndLineCap = PenLineCap.Round;

			DiffGraphBgPen.Brush = Brushes.LightGray;

			DiffGraphBg = BitmapFrame.Create(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"..\..\Diff Graph Bg.png"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

			for (int i = 0; i < 10; ++i)
			{
				DiffMediaPlayers[i] = new MediaPlayer();
				DiffMediaPlayers[i].Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"..\..\sounds\" + (i + 1) + ".mp3"));
			}

			EndMediaPlayer.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"..\..\sounds\end.mp3"));

			MovePen.Thickness = 5;
			MovePen.Brush = Brushes.LightCoral;
			MovePen.StartLineCap = PenLineCap.Round;
			MovePen.EndLineCap = PenLineCap.Round;

			CatchPen.Thickness = 5;
			CatchPen.Brush = Brushes.Green;
			CatchPen.StartLineCap = PenLineCap.Round;
			CatchPen.EndLineCap = PenLineCap.Round;

			BobblePen.Thickness = 5;
			BobblePen.Brush = Brushes.Yellow;
			BobblePen.StartLineCap = PenLineCap.Round;
			BobblePen.EndLineCap = PenLineCap.Round;

			DropPen.Thickness = 5;
			DropPen.Brush = Brushes.Red;
			DropPen.StartLineCap = PenLineCap.Round;
			DropPen.EndLineCap = PenLineCap.Round;

			DiffGraphScorePen.Thickness = 3;
			DiffGraphScorePen.Brush = Brushes.Black;
			DiffGraphScorePen.StartLineCap = PenLineCap.Round;
			DiffGraphScorePen.EndLineCap = PenLineCap.Round;

			#region Wheel Names
			//WheelNames.Add("Randy S");
			//WheelNames.Add("Mike E");
			//WheelNames.Add("Emma K");

			//WheelNames.Add("Lori D");
			//WheelNames.Add("Gerry G");
			//WheelNames.Add("Ryan Y");

			//WheelNames.Add("James W");
			//WheelNames.Add("Jay M");
			//WheelNames.Add("Mike G");

			//WheelNames.Add("Bob B");
			//WheelNames.Add("Toddy B");
			//WheelNames.Add("Beast");

			//WheelNames.Add("Mary L");
			//WheelNames.Add("Larry I");
			//WheelNames.Add("Char P");

			//WheelNames.Add("Johnny T");
			//WheelNames.Add("Neil T");
			//WheelNames.Add("Matt G");

			//WheelNames.Add("Lisa H");
			//WheelNames.Add("Charles L");
			//WheelNames.Add("Arthur C");

			//WheelNames.Add("Jake G");
			//WheelNames.Add("Tony P");
			//WheelNames.Add("Cindy S");
			//WheelNames.Add("Bill W");
			#endregion

#if !DEBUG
			if (Properties.Settings.Default.LastJudgeIndex <= 4)
			{
				JudgersTabControl.SelectedIndex = Properties.Settings.Default.LastJudgeIndex;
				TwoPointJudgeId = Properties.Settings.Default.LastJudgeId;

				if (Properties.Settings.Default.LastJudgeIndex > 0)
					StartClient();

				WindowState = System.Windows.WindowState.Maximized;
			}
#endif
		}

		private void NotifyPropertyChanged(String propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		private void CountScore_Click(object sender, RoutedEventArgs e)
		{
			Button ClickedButton = e.Source as Button;

			string Text = ClickedButton.Content as string;
			if (Text.Contains("EDITING"))
			{
				if (CurEditingBut != null)
					CurEditingBut.Content = Text.Replace("EDITING - ", "");

				CurEditingBut = null;
			}
			else
			{
				if (CurEditingBut != null)
					CurEditingBut.Content = Text.Replace("EDITING - ", "");

				ClickedButton.Content = "EDITING - " + Text;
				CurEditingBut = ClickedButton;

				CounterText.Text = Text.Substring(0, Text.IndexOf(' '));
			}
		}

		void CreateCounterScoreButton(string InCountText, string InFinishText, Brush InBrush)
		{
			if (!bRoutineRecording)
				return;

			Button NewBut = new Button();
			NewBut.MinWidth = 20;
			NewBut.Margin = new Thickness(1, 0, 1, 0);
			NewBut.Padding = new Thickness(3);
			NewBut.Content = InCountText + " " + InFinishText;
			NewBut.Click += new RoutedEventHandler(CountScore_Click);
			NewBut.Background = InBrush;
			CountStack.Children.Add(NewBut);

			CountScrollViewer.ScrollToRightEnd();

			CounterData cd = CounterData.DeSerialise(InCountText + "," + InFinishText);
			cd.RoutineTime = SecondsIntoRoutine;
			CurJudgeCounterData.CountList.Add(cd);

			// networking
			if (bIsClientConnected)
			{
				MoveNetData mnd = new MoveNetData(1, cd.RoutineTime);
				CurJudgeCounterData.MoveList.Add(mnd);

				SendToServerNetData("MoveResult", mnd);

				SendToServerNetData("CountResult", cd);

				if (bWaitingResults)
					CounterFinishedButton_Click(null, null);
			}
		}

		private void CounterKeyPad_Click(object sender, RoutedEventArgs e)
		{
			if (!bRoutineRecording)
				return;

			Button ClickedButton = e.Source as Button;

			if (ClickedButton.Content as string == "Catch")
			{
				if (CurEditingBut == null)
					CreateCounterScoreButton(CounterText.Text, "Catch", Brushes.LightGreen);
				else
				{
					CurEditingBut.Content = CounterText.Text + " Catch";
					CurEditingBut.Background = Brushes.LightGreen;
					CurEditingBut = null;
				}

				CounterText.Text = "0";
			}
			else if (ClickedButton.Content as string == "Drop")
			{
				if (CurEditingBut == null)
					CreateCounterScoreButton(CounterText.Text, "Drop", Brushes.LightSalmon);
				else
				{
					CurEditingBut.Content = CounterText.Text + " Drop";
					CurEditingBut.Background = Brushes.LightSalmon;
					CurEditingBut = null;
				}

				CounterText.Text = "0";
			}
			else if (ClickedButton.Content as string == "Bobble")
			{
				if (CurEditingBut == null)
					CreateCounterScoreButton(CounterText.Text, "Bobble", Brushes.Yellow);
				else
				{
					CurEditingBut.Content = CounterText.Text + " Bobble";
					CurEditingBut.Background = Brushes.Yellow;
					CurEditingBut = null;
				}

				CounterText.Text = "0";
			}
			else if (ClickedButton.Content as string == "+")
			{
				if (bRoutineRecording)
				{
					int CounterScore = 0;
					if (int.TryParse(CounterText.Text, out CounterScore) && CounterScore < MaxCounterScore)
						CounterText.Text = (CounterScore + 1).ToString();

					MoveNetData mnd = new MoveNetData(SecondsIntoRoutine < TotalRoutineSeconds - .001 ? 1 : 0, SecondsIntoRoutine);
					CurJudgeCounterData.MoveList.Add(mnd);

					if (bIsClientConnected)
						SendToServerNetData("MoveResult", mnd);
				}
			}
			else
			{
				CounterText.Text = ClickedButton.Content as string;
			}
		}

		private void StartCounterJudge()
		{
			if (!bRoutineRecording)
			{
				bRoutineRecording = true;
				StartTime = DateTime.Now;

				CountStack.Children.Clear();
			}
			else
			{
				bRoutineRecording = false;

				string RoutineNumberString = RoutineNumberButton.Content as string;
				int RoutineNumber = -1;
				if (int.TryParse(RoutineNumberString.Replace(" - Click To Change", ""), out RoutineNumber))
				{
					CountSaveResults(RoutineNumber);

					RoutineNumberButton.Content = (RoutineNumber + 1).ToString() + " - Click To Change";
				}
			}
		}

		void CountSaveResults(int RoutineNumber)
		{
			if (SaveFolderPath.Length > 0)
			{
				string SaveName = SaveFolderPath + "\\Count-" + RoutineNumber.ToString() + ".txt";
				char AdditionalChar = (char)0;
				while (true)
				{
					if (File.Exists(SaveName))
					{
						string FileName = SaveName.Substring(SaveFolderPath.Length + 1).Replace(".txt", "");
						if (AdditionalChar != 0)
						{
							AdditionalChar = (char)(AdditionalChar + 1);
							SaveName = SaveFolderPath + "\\Count-" + RoutineNumber.ToString() + " - " + AdditionalChar + ".txt";
						}
						else
						{
							SaveName = SaveFolderPath + "\\Count-" + RoutineNumber.ToString() + " - a.txt";
							AdditionalChar = 'a';
						}
					}
					else
						break;
				}

				StreamWriter OutFile = new StreamWriter(SaveName);
				OutFile.WriteLine(DateTime.Now.ToString());

				foreach (Button but in CountStack.Children)
				{
					string Score = but.Content as string;
					Score = Score.Replace("EDITING - ", "");

					OutFile.WriteLine(Score);
				}

				OutFile.Close();
			}
		}

		void MusicalitySaveResults()
		{
			if (SaveFolderPath.Length > 0)
			{
				string SaveName = SaveFolderPath + "\\Music.txt";
				char AdditionalChar = (char)0;
				while (true)
				{
					if (File.Exists(SaveName))
					{
						string FileName = SaveName.Substring(SaveFolderPath.Length + 1).Replace(".txt", "");
						if (AdditionalChar != 0)
						{
							AdditionalChar = (char)(AdditionalChar + 1);
							SaveName = SaveFolderPath + "\\Music - " + AdditionalChar + ".txt";
						}
						else
						{
							SaveName = SaveFolderPath + "\\Music - a.txt";
							AdditionalChar = 'a';
						}
					}
					else
						break;
				}

				StreamWriter OutFile = new StreamWriter(SaveName);
				OutFile.WriteLine(DateTime.Now.ToString());

				OutFile.WriteLine(MusicSlider0.Value);

				OutFile.Close();
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void MenuItem_SetResultFolder(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
			fbd.ShowDialog();
			if (fbd.SelectedPath.Length > 0)
			{
				SaveFolderPath = fbd.SelectedPath;
				Properties.Settings.Default.ResultsFolderPath = SaveFolderPath;
				Properties.Settings.Default.Save();
			}
		}

		private void MenuItem_Exit(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RoutineNumberButton_Click(object sender, RoutedEventArgs e)
		{
			CurrentRoutineIndex = -1;
			bEnteringRoutineNumber = true;
            CurrentEnteringRoutineNumber = 0;
		}

		private void MainWindowObj_KeyDown(object sender, KeyEventArgs e)
		{
			if (bEnteringRoutineNumber)
			{
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                {
                    if (CurrentEnteringRoutineNumber > 0)
                    {
                        CurrentEnteringRoutineNumber *= 10;
                    }

                    CurrentEnteringRoutineNumber += ((int)e.Key) - ((int)Key.D0);
                }
                else
                {
                    CurrentRoutineIndex = Math.Min(CurrentEnteringRoutineNumber - 1, PoolScores.AllRoutineScores.Count - 1);

                    bEnteringRoutineNumber = false;

                    e.Handled = true;
                }
			}
		}

		void CreateDiffScoreButton(EFinish InFinish, Brush InBrush)
		{
			Button NewBut = new Button();
			NewBut.MinWidth = 20;
			NewBut.Margin = new Thickness(1, 0, 1, 0);
			NewBut.Padding = new Thickness(3);
			NewBut.Content = CurDiffScore.TotalDiff.ToString() + " " + InFinish.ToString();
			NewBut.Click += new RoutedEventHandler(DiffScore_Click);
			NewBut.Background = InBrush;
			CurDiffScore.FinishState = InFinish;
			CurDiffScore.ScoreButton = NewBut;
			//DiffStack.Children.Add(NewBut);
			DiffScoreResults.Add(CurDiffScore);

			CurDiffScore = new DiffScoreData();
			//DiffText.Text = "";

			//DiffResultsScrollViewer.ScrollToRightEnd();
		}

		private void DiffScore_Click(object sender, RoutedEventArgs e)
		{
			Button ClickedButton = e.Source as Button;

			foreach (DiffScoreData dsd in DiffScoreResults)
			{
				if (dsd.ScoreButton == ClickedButton)
				{
					if (dsd != CurDiffScore && CurDiffScore.bEditing)
					{
						CurDiffScore.ScoreButton.Content = (CurDiffScore.ScoreButton.Content as string).Replace("EDITING - ", "");
						CurDiffScore.bEditing = false;
						CurDiffScore = new DiffScoreData();
						//DiffText.Text = CurDiffScore.ToString();
					}

					if (dsd.bEditing)
					{
						CurDiffScore.ScoreButton.Content = (CurDiffScore.ScoreButton.Content as string).Replace("EDITING - ", "");
						dsd.bEditing = false;
						CurDiffScore = new DiffScoreData();
						//DiffText.Text = CurDiffScore.ToString();
					}
					else
					{
						CurDiffScore = dsd;
						CurDiffScore.ScoreButton.Content = "EDITING - " + CurDiffScore.ScoreButton.Content;
						CurDiffScore.bEditing = true;
						//DiffText.Text = CurDiffScore.ToString();
					}
					break;
				}
			}
		}

		private void DiffResultsButton_Click(object sender, RoutedEventArgs e)
		{
			if (!bRoutineRecording)
			{
				bRoutineRecording = true;
				StartTime = DateTime.Now;
				//DiffResultsButton.Background = Brushes.Red;
				//DiffResultsButton.Content = "Finish";

				//DiffStack.Children.Clear();
				DiffScoreResults.Clear();
			}
			else
			{
				bRoutineRecording = false;
				//DiffResultsButton.Background = Brushes.Green;
				//DiffResultsButton.Content = "Start";

				string RoutineNumberString = RoutineNumberButton.Content as string;
				int RoutineNumber = -1;
				if (int.TryParse(RoutineNumberString.Replace(" - Click To Change", ""), out RoutineNumber))
				{
					DiffSaveResults(RoutineNumber);

					RoutineNumberButton.Content = (RoutineNumber + 1).ToString() + " - Click To Change";
				}
			}
		}

		void DiffSaveResults(int RoutineNumber)
		{
			if (SaveFolderPath.Length > 0)
			{
				string SaveName = SaveFolderPath + "\\Diff-" + RoutineNumber.ToString() + ".txt";
				char AdditionalChar = (char)0;
				while (true)
				{
					if (File.Exists(SaveName))
					{
						string FileName = SaveName.Substring(SaveFolderPath.Length + 1).Replace(".txt", "");
						if (AdditionalChar != 0)
						{
							AdditionalChar = (char)(AdditionalChar + 1);
							SaveName = SaveFolderPath + "\\Diff-" + RoutineNumber.ToString() + " - " + AdditionalChar + ".txt";
						}
						else
						{
							SaveName = SaveFolderPath + "\\Diff-" + RoutineNumber.ToString() + " - a.txt";
							AdditionalChar = 'a';
						}
					}
					else
						break;
				}

				StreamWriter OutFile = new StreamWriter(SaveName);
				OutFile.WriteLine(DateTime.Now.ToString());

				foreach (DiffScoreData dsd in DiffScoreResults)
				{
					OutFile.WriteLine(dsd.ToString());
				}

				OutFile.Close();
			}
		}

		void AISaveResults(int RoutineNumber)
		{
			if (SaveFolderPath.Length > 0)
			{
				string SaveName = SaveFolderPath + "\\AI-" + RoutineNumber.ToString() + ".txt";
				char AdditionalChar = (char)0;
				while (true)
				{
					if (File.Exists(SaveName))
					{
						string FileName = SaveName.Substring(SaveFolderPath.Length + 1).Replace(".txt", "");
						if (AdditionalChar != 0)
						{
							AdditionalChar = (char)(AdditionalChar + 1);
							SaveName = SaveFolderPath + "\\AI-" + RoutineNumber.ToString() + " - " + AdditionalChar + ".txt";
						}
						else
						{
							SaveName = SaveFolderPath + "\\AI-" + RoutineNumber.ToString() + " - a.txt";
							AdditionalChar = 'a';
						}
					}
					else
						break;
				}

				StreamWriter OutFile = new StreamWriter(SaveName);
				OutFile.WriteLine(DateTime.Now.ToString());

				OutFile.WriteLine(AiButton0.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton1.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton2.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton3.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton4.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton5.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton6.Background == Brushes.LightGreen ? 1 : 0);
				OutFile.WriteLine(AiButton7.Background == Brushes.LightGreen ? 1 : 0);

				OutFile.Close();
			}
		}

		private void AIStartButton_Click(object sender, RoutedEventArgs e)
		{
			if (bRoutineRecording)
			{
				bRoutineRecording = false;

				string RoutineNumberString = RoutineNumberButton.Content as string;
				int RoutineNumber = -1;
				if (int.TryParse(RoutineNumberString.Replace(" - Click To Change", ""), out RoutineNumber))
				{
					AISaveResults(RoutineNumber);

					RoutineNumberButton.Content = (RoutineNumber + 1).ToString() + " - Click To Change";
				}
			}
			else
			{
				bRoutineRecording = true;

				AiButton0.Background = Brushes.LightGray;
				AiButton1.Background = Brushes.LightGray;
				AiButton2.Background = Brushes.LightGray;
				AiButton3.Background = Brushes.LightGray;
				AiButton4.Background = Brushes.LightGray;
				AiButton5.Background = Brushes.LightGray;
				AiButton6.Background = Brushes.LightGray;
				AiButton7.Background = Brushes.LightGray;
			}
		}

		private void AI_Click(object sender, RoutedEventArgs e)
		{
			if (!bRoutineRecording)
			{
				return;
			}

			Button But = e.Source as Button;
			bool IsChecked = But.Background != Brushes.LightGreen;

			But.Background = IsChecked ? Brushes.LightGreen : Brushes.LightGray;

			int AiIndex = -1;
			if (int.TryParse(But.Name.Replace("AiButton", ""), out AiIndex))
			{
				CurJudgeAiData.AiScores[AiIndex] = IsChecked;
			}

			SendToServerNetData("AiResult", But.Name + (IsChecked ? ",true" : ",false"));
		}

		private void RecordButton_Click(object sender, RoutedEventArgs e)
		{
			MusicalitySaveResults();
		}

		private void JudgerChooser_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 0;

			Process.Start(Application.ResourceAssembly.Location);
			Application.Current.Shutdown();
		}

		private void HatSorter_Click(object sender, RoutedEventArgs e)
		{
			HatSorterWindow.Show();

			HatSorterWindow.Activate();
		}

		private void CounterButton_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 1;

#if !DEBUG
			WindowState = System.Windows.WindowState.Maximized;
#endif

			StartClient();
		}

		private void DiffButton_Click(object sender, RoutedEventArgs e)
		{
			TwoPointJudgeId = (sender as Button).Tag as string;

			JudgersTabControl.SelectedIndex = 2;

#if !DEBUG
			WindowState = System.Windows.WindowState.Maximized;
#endif

			StartClient();
		}

		private void AIButton_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 3;

#if !DEBUG
			WindowState = System.Windows.WindowState.Maximized;
#endif

			StartClient();
		}

		private void MusicButton_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 4;

#if !DEBUG
			WindowState = System.Windows.WindowState.Maximized;
#endif

			StartClient();
		}

		private void HeadButton_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 5;

			LoadScoresButton_Click(null, null);

			StartServer();
		}

		private void ScoreboardButton_Click(object sender, RoutedEventArgs e)
		{
			JudgersTabControl.SelectedIndex = 6;

#if !DEBUG
			WindowState = System.Windows.WindowState.Maximized;
#endif

			StartScoreboardClient();
		}

		void Touch_FrameReported(object sender, TouchFrameEventArgs e)
		{
			TouchPoint tp = e.GetPrimaryTouchPoint(DiffCanvas);
			if (tp != null)
			{
				HitTestResult Result = VisualTreeHelper.HitTest(DiffCanvas, tp.Position);

				if (Result != null)
				{
					//bIsDiffTouch = true;
					MousePos = tp.Position;
				}
			}
		}

		private void DiffCanvas_MouseDown(object sender, MouseButtonEventArgs e)
		{
			bIsDiffMouse = true;

			MousePos = e.GetPosition(DiffCanvas);
		}

		private void DiffCanvas_MouseUp(object sender, MouseButtonEventArgs e)
		{
			bIsDiffMouse = false;
		}

		private void DiffCanvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (bIsDiffMouse)
				MousePos = e.GetPosition(DiffCanvas);
		}

		private void HeadJudgeStartButton_Click(object sender, RoutedEventArgs e)
		{
			if (bRoutineRecording)
			{
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    FinishRoutineServer(true);
                }
			}
			else if (NetworkComms.AllConnectionInfo().Count > 1)
			{
				ResetSeverScores();

				bRoutineRecording = true;
				bWaitingResults = true;
				bWaitingForReady = false;
				HeadJudgeStartButton.Background = Brushes.LightSalmon;
				HeadJudgeStartButton.Content = "Hold Control To Cancel";
				FinishedJudgerFlags = 0;

				RoutineStartTime = DateTime.Now;

                SendToClientsNetData("StartRoutine", RoutineMinutesLength);
			}
		}

		public void EndRoutineTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			bWaitingResults = true;
			bWaitingForReady = false;

			Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Threading.ThreadStart(() =>
			{
				if (bSoundEnabled)
				{
					EndMediaPlayer.Stop();
					EndMediaPlayer.Play();
				}

				TwoPointFinishRoutine();

                FinishTwoPoint();
			}));
		}

		public void SetFinishedButtons(EJudgerState InJudgeState)
		{
			string ButStr = "";
			Brush BgColor = Brushes.White;
			switch (InJudgeState)
			{
				case EJudgerState.Ready:
					ButStr = "Waiting";
					BgColor = Brushes.LightGray;
					break;
				case EJudgerState.Judging:
					ButStr = "Judging";
					BgColor = Brushes.LightYellow;
					break;
				case EJudgerState.Finishing:
					ButStr = "Finish";
					BgColor = Brushes.LightGreen;
					break;
			}

			MusicFinishedButton.Content = ButStr;
			MusicFinishedButton.Background = BgColor;
			AiFinishedButton.Content = ButStr;
			AiFinishedButton.Background = BgColor;
			DiffFinishedButton.Content = ButStr;
			DiffFinishedButton.Background = BgColor;
			CounterFinishedButton.Content = ButStr;
			CounterFinishedButton.Background = BgColor;
		}

		public void SetJudgeInterfaceEnabled(bool InEnabled)
		{
			KeyPadInc.IsEnabled = InEnabled;
			KeyPadBobble.IsEnabled = InEnabled;
			KeyPadDrop.IsEnabled = InEnabled;
			KeyPadCatch.IsEnabled = InEnabled;

			AiButton0.IsEnabled = InEnabled;
			AiButton1.IsEnabled = InEnabled;
			AiButton2.IsEnabled = InEnabled;
			AiButton3.IsEnabled = InEnabled;
			AiButton4.IsEnabled = InEnabled;
			AiButton5.IsEnabled = InEnabled;
			AiButton6.IsEnabled = InEnabled;
			AiButton7.IsEnabled = InEnabled;

			MusicSlider0.IsEnabled = InEnabled;
		}

		private void MusicFinishedButton_Click(object sender, RoutedEventArgs e)
		{
			if (bWaitingResults)
			{
				bWaitingResults = false;
				bWaitingForReady = true;
				bRoutineRecording = false;

				SendToServerNetData("C2SFinished", 8);

				CurJudgeMusicData.SendBackup();
				WriteBackupToDisk();
			}
		}

		private void AiFinishedButton_Click(object sender, RoutedEventArgs e)
		{
			if (bWaitingResults)
			{
				bWaitingResults = false;
				bWaitingForReady = true;
				bRoutineRecording = false;

				SendToServerNetData("C2SFinished", 4);

				CurJudgeAiData.SendBackup();
				WriteBackupToDisk();
			}
		}

		private void DiffFinishedButton_Click(object sender, RoutedEventArgs e)
		{
			if (bWaitingResults)
			{
                FinishTwoPoint();
			}
		}

        private void FinishTwoPoint()
        {
            bWaitingResults = false;
            bWaitingForReady = true;
            bRoutineRecording = false;

            SendToServerNetData("C2SFinished", TwoPointJudgeId);

            CurJudgeDiffData.SendBackup();
            WriteBackupToDisk();
        }

		private void CounterFinishedButton_Click(object sender, RoutedEventArgs e)
		{
			if (bWaitingResults)
			{
				bWaitingResults = false;
				bWaitingForReady = true;
				bRoutineRecording = false;

				SendToServerNetData("C2SFinished", 1);

				CurJudgeCounterData.SendBackup();
				WriteBackupToDisk();
			}
		}

		private void ToggleScoreboard_Click(object sender, RoutedEventArgs e)
		{
			bShowingScoreboardRandom = false;
			bShowingRankingScoreboard = !bShowingRankingScoreboard;

			SendToScoreboardsNetData("ToggleScoreboard", bShowingRankingScoreboard);
		}

		private void ToggleScoreboardRandom_Click(object sender, RoutedEventArgs e)
		{
			bShowingRankingScoreboard = false;
			bShowingScoreboardRandom = !bShowingScoreboardRandom;

			SendToScoreboardsNetData("ToggleScoreboardRandom", bShowingScoreboardRandom);
		}

		private void StartHatPicking_Click(object sender, RoutedEventArgs e)
		{
			if (bShowingScoreboardRandom)
			{
				SendToScoreboardsNetData("StartWheelSpin", "");
			}
		}

		public void UpdateSortedResults(RoutineScore InScore)
		{
			RoutineScore CurrentScore = null;
			foreach (RoutineScore rs in SortedResults)
			{
				if (rs.TeamNameTrim == InScore.TeamNameTrim)
				{
					CurrentScore = rs;
					break;
				}
			}

			if (CurrentScore != null)
				SortedResults.Remove(CurrentScore);
			else
			{
				for (int ResultIndex = 0; ResultIndex < SortedResults.Count; ++ResultIndex)
				{
					if (SortedResults[ResultIndex].TeamName == InScore.TeamName)
					{
						SortedResults.RemoveAt(ResultIndex);
						break;
					}
				}
			}

			int InsertIndex = 0;
			for (; InsertIndex < SortedResults.Count; ++InsertIndex)
			{
				if (SortedResults[InsertIndex].TotalScore < InScore.TotalScore)
					break;
			}

			SortedResults.Insert(InsertIndex, InScore);
		}

        public void FinishRoutineServer()
        {
            FinishRoutineServer(false);
        }

		public void FinishRoutineServer(bool bCancel)
		{
			RoutineScore NewScore = new RoutineScore(
				CalcJudgeTwoPointScore("Judge1"), CalcJudgeTwoPointScore("Judge2"), CalcJudgeTwoPointScore("Judge3"), CurSplits, true);

			if (CurrentRoutineIndex < PoolScores.AllRoutineScores.Count)
			{
				NewScore.TeamName = PoolScores.AllRoutineScores[CurrentRoutineIndex].TeamName;
				PoolScores.AllRoutineScores[CurrentRoutineIndex] = NewScore;
			}
			else
				PoolScores.AllRoutineScores.Add(NewScore);

			if (LeaderScore == null || NewScore.TotalScore > LeaderScore.TotalScore)
			{
				LeaderScore = NewScore;

				SendToScoreboardsNetData("LeaderData",
					new LeaderNetData(LeaderScore.TeamName, LeaderScore.TotalScore));
			}

			// Update the sorted results
			if (CurrentRoutineIndex < PoolScores.AllRoutineScores.Count)
				UpdateSortedResults(PoolScores.AllRoutineScores[CurrentRoutineIndex]);

            if (!bCancel)
            {
                if (CurrentRoutineIndex + 1 < PoolScores.AllRoutineScores.Count)
                    ++CurrentRoutineIndex;
            }

			bResultsDirty = true;

			ResetRoutine(bCancel);

			ResetSeverScores();

			SendRankingsToAllScoreboards();

			SavePoolData();
		}

		public void ResetJudgers()
		{
			ResetTwoPoint();
			DiffScoreResults.Clear();
			CounterText.Text = "0";
			CountStack.Children.Clear();
			AiButton0.Background = Brushes.LightGray;
			AiButton1.Background = Brushes.LightGray;
			AiButton2.Background = Brushes.LightGray;
			AiButton3.Background = Brushes.LightGray;
			AiButton4.Background = Brushes.LightGray;
			AiButton5.Background = Brushes.LightGray;
			AiButton6.Background = Brushes.LightGray;
			AiButton7.Background = Brushes.LightGray;
			MusicSlider0.Value = 0;

			string JudgeRoutineName = JudgeRoutineText;
			Regex NumberReg = new Regex(@"^\d+.\s");
			Match NumberMatch = NumberReg.Match(JudgeRoutineName);
			if (NumberMatch.Success)
				JudgeRoutineName = JudgeRoutineName.Replace(NumberMatch.Value, "");

			switch (JudgersTabControl.SelectedIndex)
			{
				case 1:
					{
						if (BackupData.BackupCountData.ContainsKey(JudgeRoutineName))
						{
							CurJudgeCounterData = BackupData.BackupCountData[JudgeRoutineName] = new JudgeCountData(JudgeRoutineName);
						}
						else
						{
							CurJudgeCounterData = new JudgeCountData(JudgeRoutineName);
							BackupData.BackupCountData.Add(CurJudgeCounterData.RoutineName, CurJudgeCounterData);
						}
						CurJudgeCounterData.bTransfered = false;
						break;
					}
				case 2:
					{
						if (BackupData.BackupDiffData.ContainsKey(JudgeRoutineName))
						{
							CurJudgeDiffData = BackupData.BackupDiffData[JudgeRoutineName] = new JudgeDiffData(JudgeRoutineName);
						}
						else
						{
							CurJudgeDiffData = new JudgeDiffData(JudgeRoutineName);
							BackupData.BackupDiffData.Add(CurJudgeDiffData.RoutineName, CurJudgeDiffData);
						}
						CurJudgeDiffData.bTransfered = false;
						break;
					}
				case 3:
					{
						if (BackupData.BackupAiData.ContainsKey(JudgeRoutineName))
						{
							CurJudgeAiData = BackupData.BackupAiData[JudgeRoutineName] = new JudgeAiData(JudgeRoutineName);
						}
						else
						{
							CurJudgeAiData = new JudgeAiData(JudgeRoutineName);
							BackupData.BackupAiData.Add(CurJudgeAiData.RoutineName, CurJudgeAiData);
						}
						CurJudgeAiData.bTransfered = false;
						break;
					}
				case 4:
					{
						if (BackupData.BackupMusicData.ContainsKey(JudgeRoutineName))
						{
							CurJudgeMusicData = BackupData.BackupMusicData[JudgeRoutineName] = new JudgeMusicData(JudgeRoutineName);
						}
						else
						{
							CurJudgeMusicData = new JudgeMusicData(JudgeRoutineName);
							BackupData.BackupMusicData.Add(CurJudgeMusicData.RoutineName, CurJudgeMusicData);
						}
						CurJudgeMusicData.bTransfered = false;
						break;
					}
			}
		}

		public void ResetSeverScores()
		{
			ResultsSema.WaitOne();

			CurComboScore = new ComboScore();
			CurRoutineComboScores = new List<ComboScore>();
			AiScores = Enumerable.Repeat(false, 8).ToArray();
			RecievedMusicScore = 0;
			RecievedAiData.Clear();
			RecievedCounterData.Clear();
			RecievedDiffData.Clear();
			RecievedMoveData.Clear();
			SplitInterval = -1;
			CurSplit = new SplitData();
			CurSplits = new List<SplitData>();
			foreach (string key in RecievedDiffDataLastSentToScoreboard.Keys.ToList())
			{
				RecievedDiffDataLastSentToScoreboard[key] = 0;
			}

			if (bIsScoreboard)
			{
				CurrentDiff = 0;
				CurrentAi = 0;
				CurrentMusic = 0;

				LeaderSplitDiff = 0;
				LeaderSplitAi = 0;
				LeaderSplitMusic = 0;

				CurrentSplitDiff = 0;
				CurrentSplitAi = 0;
				CurrentSplitMusic = 0;

				LeaderSplitJudge1 = 0;
				LeaderSplitJudge2 = 0;
				LeaderSplitJudge3 = 0;

				currentData = new SplitData();
				currentSplit = new SplitData();
				leaderSplit = new SplitData();
			}

			ResultsSema.Release();
		}

		public void UpdateTeamInfo()
		{
			TeamsTextBox.Text = "";
			int TeamIndex = 1;

			foreach (RoutineScore rs in PoolScores.AllRoutineScores)
			{
				if (TeamIndex - 1 == CurrentRoutineIndex)
					TeamsTextBox.Text += "==> ";

				TeamsTextBox.Text += TeamIndex + ". " + rs.TeamName + "\r\n";
				++TeamIndex;
			}

			SendToClientsNetData("TeamInfo", CurrentRoutineIndex >= 0 ? RoutineButtonText : "Waiting");
		}

		private void SetTeamsButton_Click(object sender, RoutedEventArgs e)
		{
			TeamsTextBox.Text = TeamsTextBox.Text.Replace("==> ", "");

			StringReader RawText = new StringReader(TeamsTextBox.Text);
			Regex NumberRegex = new Regex(@"^\d\.\s*");
			int RoutineNumber = 0;

			string line = null;
			while ((line = RawText.ReadLine()) != null)
			{
				Match NumberMatch = NumberRegex.Match(line);
				if (NumberMatch.Success)
				{
					line = line.Replace(NumberMatch.Value, "");
				}

				if (line.Length > 0)
				{
					if (RoutineNumber < PoolScores.AllRoutineScores.Count)
						PoolScores.AllRoutineScores[RoutineNumber].TeamName = line;
					else
						PoolScores.AllRoutineScores.Add(new RoutineScore(line));

					++RoutineNumber;
				}
			}

			RawText.Close();

			CurrentRoutineIndex = 0;
		}

		private void SaveScoresButton_Click(object sender, RoutedEventArgs e)
		{
			SavePoolData();
		}

		public void SavePoolData()
		{
			if (SaveFolderPath.Length > 0)
			{
				bResultsDirty = false;

				XmlSerializer serializer = new XmlSerializer(typeof(PoolData));

				try
				{
					string BackupFolderPath = SaveFolderPath + "\\BackupSaves";
					if (!Directory.Exists(BackupFolderPath))
						Directory.CreateDirectory(BackupFolderPath);

					string asdf = DateTime.Now.ToFileTime().ToString();
					StreamWriter stream = new StreamWriter(BackupFolderPath + "\\PoolData - " + asdf + ".xml");
					serializer.Serialize(stream, PoolScores);
					stream.Close();
				}
				catch
				{
				}

				try
				{
					StreamWriter stream = new StreamWriter(SaveFolderPath + "\\PoolData.xml");
					serializer.Serialize(stream, PoolScores);
					stream.Close();
				}
				catch
				{
				}
			}
		}

		private void LoadScoresButton_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.InitialDirectory = SaveFolderPath;
			ofd.Multiselect = false;

			//if (ofd.ShowDialog(this) == true)
			{
				try
				{
					string filename = SaveFolderPath + "\\PoolData.xml";
					//string filename = ofd.FileName;
					XmlSerializer serializer = new XmlSerializer(typeof(PoolData));
					FileStream stream = new FileStream(filename, FileMode.Open);
					PoolScores = serializer.Deserialize(stream) as PoolData;
					stream.Close();

					TeamsTextBox.Text = "";
					foreach (RoutineScore rs in PoolScores.AllRoutineScores)
					{
						TeamsTextBox.Text += rs.TeamName + "\r\n";
					}

					SetTeamsButton_Click(null, null);

					bResultsDirty = false;

					foreach (RoutineScore rs in PoolScores.AllRoutineScores)
					{
						if (LeaderScore == null || rs.TotalScore > LeaderScore.TotalScore)
							LeaderScore = rs;
					}

					if (LeaderScore != null)
					{
						SendToScoreboardsNetData("LeaderData",
							new LeaderNetData(LeaderScore.TeamName, LeaderScore.TotalScore));
					}

					SortedResults.Clear();
					foreach (RoutineScore rs in PoolScores.AllRoutineScores)
					{
						if (rs.TotalScore > 0)
							UpdateSortedResults(rs);
					}
				}
				catch
				{
				}
			}
		}

		private void ClearScoresButton_Click(object sender, RoutedEventArgs e)
		{
			PoolScores.AllRoutineScores.Clear();

			SortedResults.Clear();

			bResultsDirty = false;
		}

		private void MainWindowObj_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			double CanvasWidth = DiffGraphCanvas.ActualWidth;
			double CanvasHeight = DiffGraphCanvas.ActualHeight;
			if (JudgersTabControl.SelectedIndex != 6 || CanvasWidth <= 0 || CanvasHeight <= 0)
				DiffGraphBitmap = null;
			else if (JudgersTabControl.SelectedIndex != 8 || WheelCanvas.ActualWidth <= 0 || WheelCanvas.ActualHeight <= 0)
				WheelBitmap = null;
		}

		public void ResetRoutine(bool bInCancel)
		{
			bRoutineRecording = false;
			bWaitingResults = false;
			bWaitingForReady = true;
			HeadJudgeStartButton.Background = Brushes.LightGreen;
			HeadJudgeStartButton.Content = "Start Routine";
			FinishedJudgerFlags = 0;

			if (bInCancel)
			{
				SendToClientsNetData("CancelRoutine", DateTime.Now.ToString());
			}
		}

		public bool AddToBackup<T>(ref IDictionary<string, T> InBackup, ref List<T> InAdditions) where T : JudgeDataBase
		{
			bool Ret = false;
			foreach (T data in InAdditions)
			{
				data.ConfirmBackup();

				if (InBackup.ContainsKey(data.RoutineName))
					InBackup[data.RoutineName] = data;
				else
					InBackup.Add(data.RoutineName, data);

				Ret = true;
			}
			InAdditions.Clear();

			return Ret;
		}

		public bool MarkTransfered<T>(ref IDictionary<string, T> InBackup, ref List<string> InTransfers) where T : JudgeDataBase
		{
			bool bDirty = false;
			foreach (string RoutineName in InTransfers)
			{
				if (InBackup.ContainsKey(RoutineName))
				{
					InBackup[RoutineName].bTransfered = true;
					bDirty = true;
				}
			}
			InTransfers.Clear();

			return bDirty;
		}

		public string GetBackupFilename()
		{
			switch (JudgersTabControl.SelectedIndex)
			{
				case 1:
					return "Backup-Count.bin";
				case 2:
					return "Backup-Diff.bin";
				case 3:
					return "Backup-Ai.bin";
				case 4:
					return "Backup-Music.bin";
				case 5:
					return "Backup-HeadJudge.bin";
			}

			return "Backup-Error.bin";
		}

		public void WriteBackupToDisk()
		{
            try
            {
                using (var NewFile = File.Create(SaveFolderPath + "\\" + GetBackupFilename()))
                {
                    Serializer.Serialize(NewFile, BackupData);
                }
            }
            catch
            {
            }
		}

		public void LoadBackup()
		{
            try
            {
                string Filename = SaveFolderPath + "\\" + GetBackupFilename();
                if (File.Exists(Filename))
                {
                    using (var NewFile = File.OpenRead(Filename))
                    {
                        BackupData = Serializer.Deserialize<JudgeBackupData>(NewFile);
                        BackupData.CalcCompletedRoutines();

                        RebuildBackupDisplayList();
                    }
                }
            }
            catch
            {
            }
		}

		void RebuildBackupDisplayList()
		{
			BackupDisplayList.Clear();
			foreach (RoutineScore backup in BackupData.CompleteRoutineScores)
			{
				RoutineScore CurrentScore = null;
				foreach (RoutineScore rs in PoolScores.AllRoutineScores)
				{
					if (rs.TeamNameTrim == backup.TeamNameTrim)
					{
						CurrentScore = rs;
						break;
					}
				}

				BackupDisplay NewDisplay = new BackupDisplay();
				NewDisplay.CurrentScore = CurrentScore;
				NewDisplay.BackupScore = backup;
				BackupDisplayList.Add(NewDisplay);
			}
		}

		private void BackupDisplayButton_Click(object sender, RoutedEventArgs e)
		{
			Button DisplayBut = sender as Button;
			if (DisplayBut != null)
			{
				foreach (RoutineScore backup in BackupData.CompleteRoutineScores)
				{
					if (backup.TeamNameTrim == DisplayBut.Tag.ToString())
					{
						for (int RoutineIndex = 0; RoutineIndex < PoolScores.AllRoutineScores.Count; ++RoutineIndex)
						{
							if (PoolScores.AllRoutineScores[RoutineIndex].TeamNameTrim == backup.TeamNameTrim)
							{
								PoolScores.AllRoutineScores[RoutineIndex] = backup;
								UpdateSortedResults(PoolScores.AllRoutineScores[RoutineIndex]);

								SendRankingsToAllScoreboards();
								return;
							}
						}

						PoolScores.AllRoutineScores.Add(backup);
						UpdateSortedResults(PoolScores.AllRoutineScores[PoolScores.AllRoutineScores.Count - 1]);
					}
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			HatSorterWindow.bAllowClosing = true;
			HatSorterWindow.Close();

			Properties.Settings.Default.LastJudgeIndex = JudgersTabControl.SelectedIndex;
			Properties.Settings.Default.LastJudgeId = TwoPointJudgeId;
			Properties.Settings.Default.Save();

			NetworkComms.Shutdown();
		}

		private void SendHatNamesButton_Click(object sender, RoutedEventArgs e)
		{
			HatTeamNetData Data = new HatTeamNetData();

			StringReader TeamsReader = new StringReader(TeamsTextBox.Text);

			string line = null;
			while ((line = TeamsReader.ReadLine()) != null)
			{
				line = line.Trim();
				if (line.Length > 0)
					Data.HatNames.Add(line);
			}

			SendToScoreboardsNetData("HatNames", Data);
		}

		private void ExportResultsButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				StreamWriter OutFile = new StreamWriter(SaveFolderPath + @"\ResultsExport.csv");

				foreach (RoutineScore rs in SortedResults)
				{
					OutFile.WriteLine(rs.TeamName + ",");
					OutFile.WriteLine("Total: " + rs.TotalScore);
					

					OutFile.WriteLine();
					OutFile.WriteLine();
				}

				OutFile.Close();
			}
			catch
			{
			}
		}

		// Networking ////////////////////////////
		void StartClient()
		{
			NetworkComms.IgnoreUnknownPacketTypes = true;

			UDPConnection.StartListening(new IPEndPoint(IPAddress.Any, 0), true);

            NetworkComms.AppendGlobalIncomingPacketHandler<ClientConnectInfoNetData>("S2CPing", RecieveSeverPing);
			NetworkComms.AppendGlobalIncomingPacketHandler<double>("StartRoutine", RecieveStartRoutine);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("CancelRoutine", RecieveCancelRoutine);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("TeamInfo", RecieveTeamInfo);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ConfirmBackupCount", RecieveConfirmBackupCount);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ConfirmBackupDiff", RecieveConfirmBackupDiff);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ConfirmBackupAi", RecieveConfirmBackupAi);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ConfirmBackupMusic", RecieveConfirmBackupMusic);

			PingServerTimer.Elapsed += new ElapsedEventHandler(ClientSearchElapsed);
			PingServerTimer.Interval = 1000;
			PingServerTimer.AutoReset = true;
			PingServerTimer.Start();

			EndRoutineTimer.Interval = 1000 * 60 * RoutineMinutesLength;
			EndRoutineTimer.AutoReset = false;
			EndRoutineTimer.Elapsed += new ElapsedEventHandler(EndRoutineTimer_Elapsed);

			ClientPingServer();

			NetStatusStr = "Connecting";
			NetStatusColor = Brushes.LightYellow;

			TickTimer.Interval = 30;
			TickTimer.Elapsed += new ElapsedEventHandler(ClientTickElapsed);
			TickTimer.Start();

			LoadBackup();
		}

		void StartScoreboardClient()
		{
			NetworkComms.IgnoreUnknownPacketTypes = true;

			UDPConnection.StartListening(new IPEndPoint(IPAddress.Any, 0), true);

            NetworkComms.AppendGlobalIncomingPacketHandler<ClientConnectInfoNetData>("S2CPing", RecieveSeverPing);
			NetworkComms.AppendGlobalIncomingPacketHandler<double>("StartRoutine", RecieveStartRoutine);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("CancelRoutine", RecieveCancelRoutine);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("TeamInfo", RecieveTeamInfo);

			NetworkComms.AppendGlobalIncomingPacketHandler<DiffNetData>("ScoreboardDiffResult", RecieveScoreboardDiffResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("AiResult", RecieveAiResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<float>("MusicResult", RecieveMusicResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<MoveNetData>("MoveResult", RecieveMoveResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<CounterData>("CountResult", RecieveCountResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<LeaderNetData>("LeaderData", RecieveLeaderData);
			NetworkComms.AppendGlobalIncomingPacketHandler<float>("CurrentDiffScore", RecieveCurrentDiffScore);
			NetworkComms.AppendGlobalIncomingPacketHandler<float>("CurrentAiScore", RecieveCurrentAiScore);
			NetworkComms.AppendGlobalIncomingPacketHandler<SplitNetData>("SplitData", RecieveCurrentSplitData);
			NetworkComms.AppendGlobalIncomingPacketHandler<bool>("ToggleScoreboard", RecieveToggleScoreboard);
			NetworkComms.AppendGlobalIncomingPacketHandler<bool>("ToggleScoreboardRandom", RecieveToggleScoreboardRandom);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("InitRankings", RecieveInitRankings);
			NetworkComms.AppendGlobalIncomingPacketHandler<RankingVisual>("AddRankedTeam", RecieveAddRankedTeam);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("AddWaitingTeam", RecieveAddWaitingTeam);
			NetworkComms.AppendGlobalIncomingPacketHandler<HatTeamNetData>("HatNames", RecieveHatNames);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("StartWheelSpin", RecieveStartWheelSpin);

			PingServerTimer.Elapsed += new ElapsedEventHandler(ClientSearchElapsed);
			PingServerTimer.Interval = 1000;
			PingServerTimer.AutoReset = true;
			PingServerTimer.Start();

			ClientPingServer();

			NetStatusStr = "Connecting";
			NetStatusColor = Brushes.LightYellow;

			TickTimer.Elapsed += new ElapsedEventHandler(ScoreboardTickElapsed);
			TickTimer.Start();

			bIsScoreboard = true;

			//WindowState = System.Windows.WindowState.Maximized;
			//WindowStyle = System.Windows.WindowStyle.None;
		}

		void StartServer()
		{
			SelfObj.NetStatusStr = "Connections";
			SelfObj.NetStatusColor = Brushes.LightSalmon;

			NetworkComms.IgnoreUnknownPacketTypes = true;

			TickTimer.Elapsed += new ElapsedEventHandler(ServerTickElapsed);
			TickTimer.Start();

			UDPConnection.StartListening(new IPEndPoint(IPAddress.Any, 20000), true);

			TCPConnection.StartListening(true);

			Console.WriteLine("Server listening for TCP connection on:");
			foreach (System.Net.IPEndPoint localEndPoint in TCPConnection.ExistingLocalListenEndPoints())
				Console.WriteLine("{0}:{1}", localEndPoint.Address, localEndPoint.Port);

			ServerAquireConnectionInfo();

			// Judges
			NetworkComms.AppendGlobalIncomingPacketHandler<CounterData>("CountResult", RecieveCountResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<DiffNetData>("DiffResult", RecieveDiffResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<MoveNetData>("MoveResult", RecieveMoveResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("AiResult", RecieveAiResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<float>("MusicResult", RecieveMusicResult);
			NetworkComms.AppendGlobalIncomingPacketHandler<int>("C2SPing", RecieveClientPing);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("C2SFinished", RecieveClientFinished);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ClientConnected", RecieveClientConnected);
			NetworkComms.AppendGlobalIncomingPacketHandler<JudgeCountData>("BackupCount", RecieveBackupCount);
			NetworkComms.AppendGlobalIncomingPacketHandler<JudgeDiffData>("BackupDiff", RecieveBackupDiff);
			NetworkComms.AppendGlobalIncomingPacketHandler<JudgeAiData>("BackupAi", RecieveBackupAi);
			NetworkComms.AppendGlobalIncomingPacketHandler<JudgeMusicData>("BackupMusic", RecieveBackupMusic);
			NetworkComms.AppendGlobalIncomingPacketHandler<TwoPointBackupData>("BackupTwoPoint", RecieveBackupTwoPoint);

			//Scoreboard
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ScoreboardConnected", RecieveScoreboardConnected);

			LoadBackup();
		}

		public static void SendToServerNetData(string InType, object InData)
		{
			try
			{
				if (bIsClientConnected)
					NetworkComms.SendObject(InType, MainWindow.ServerIpAddress, 10000, InData);
			}
			catch
			{
				NetExceptionTimer = 10;
			}
		}

		public static void SendToConnectionNetData(string InType, ConnectionInfo InInfo, object InData)
		{
			try
			{
				TCPConnection.GetConnection(InInfo).SendObject(InType, InData);
			}
			catch
			{
				NetExceptionTimer = 10;
			}
		}

		public static void SendToClientsNetData(string InType, object InData)
		{
			foreach (ConnectionInfo ci in NetworkComms.AllConnectionInfo())
			{
				if (ci.LocalEndPoint.Port == 10000)
					SendToConnectionNetData(InType, ci, InData);
			}
		}

		public static void SendToScoreboardsNetData(string InType, object InData)
		{
			foreach (ScoreboardConnectionInfo sci in ScoreboardConnections)
			{
				if (sci.bIsConnected)
				{
					SendToConnectionNetData(InType, sci.ScoreboardConnection, InData);
				}
			}
		}

		public void ClientSearchElapsed(Object sender, ElapsedEventArgs e)
		{
			ClientPingServer();
		}

		static void RecieveStartRoutine(PacketHeader InHeader, Connection InConnection, double InData)
		{
			bScoreboardRoutineStart = true;
			bInitScores = true;
			bRoutineRecording = true;
			RoutineStartTime = DateTime.Now;
            RoutineMinutesLength = InData;
            EndRoutineTimer.Interval = 1000 * 60 * RoutineMinutesLength;

			EndRoutineTimer.Start();
		}

		static void RecieveTeamInfo(PacketHeader InHeader, Connection InConnection, string InData)
		{
			RecievedTeamInfo = InData;
		}

		static void RecieveConfirmBackupCount(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			RecievedConfirmBackupCount.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveConfirmBackupDiff(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			RecievedConfirmBackupDiff.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveConfirmBackupAi(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			RecievedConfirmBackupAi.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveConfirmBackupMusic(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			RecievedConfirmBackupMusic.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveCancelRoutine(PacketHeader InHeader, Connection InConnection, string InData)
		{
			bRoutineRecording = false;
			bWaitingResults = false;
			bWaitingForReady = true;

			EndRoutineTimer.Stop();

			bInitScores = true;
		}

        static void RecieveSeverPing(PacketHeader InHeader, Connection InConnection, ClientConnectInfoNetData InData)
		{
			MainWindow.ServerIpAddress = InData.IpString;

			PingServerTimer.Stop();

			SelfObj.NetStatusStr = "Connected";
			SelfObj.NetStatusColor = Brushes.LightGreen;

            RoutineMinutesLength = InData.RoutineLengthMinutes;
            EndRoutineTimer.Interval = 1000 * 60 * RoutineMinutesLength;

			Console.WriteLine("Got server ip " + InData);

			if (bIsScoreboard)
				NetworkComms.SendObject("ScoreboardConnected", MainWindow.ServerIpAddress, 10000, DateTime.Now.ToString());
			else
				NetworkComms.SendObject("ClientConnected", MainWindow.ServerIpAddress, 10000, SelfObj.TwoPointJudgeId);
		}

		//////////////// Scoreboard ///////////////////////////
		static void RecieveScoreboardDiffResult(PacketHeader InHeader, Connection InConnection, DiffNetData InData)
		{
			ResultsSema.WaitOne();
			if (!RecievedDiffData.ContainsKey(InData.JudgeId))
			{
				RecievedDiffData.Add(InData.JudgeId, new List<DiffNetData>());
			}

			RecievedDiffData[InData.JudgeId].Add(InData);
			ResultsSema.Release();
		}

		static void RecieveLeaderData(PacketHeader InHeader, Connection InConnection, LeaderNetData InData)
		{
			ResultsSema.WaitOne();
			RecievedLeaderData = InData;
			ResultsSema.Release();
		}

		static void RecieveCurrentDiffScore(PacketHeader InHeader, Connection InConnection, float InData)
		{
			RecievedCurrentDiffScore = InData;
		}

		static void RecieveCurrentAiScore(PacketHeader InHeader, Connection InConnection, float InData)
		{
			RecievedCurrentAiScore = InData;
		}

		static void RecieveCurrentSplitData(PacketHeader InHeader, Connection InConnection, SplitNetData InData)
		{
			ResultsSema.WaitOne();
			RecievedSplitData = InData;
			ResultsSema.Release();
		}

		static void RecieveToggleScoreboard(PacketHeader InHeader, Connection InConnection, bool InToggle)
		{
			RecievedToggleScoreboard = InToggle ? 1 : 0;
			RecievedToggleScoreboardRandom = 0;
		}

		static void RecieveToggleScoreboardRandom(PacketHeader InHeader, Connection InConnection, bool InToggle)
		{
			RecievedToggleScoreboardRandom = InToggle ? 1 : 0;
		}

		static void RecieveInitRankings(PacketHeader InHeader, Connection InConnection, string InData)
		{
			bInitRankings = true;
		}

		static void RecieveAddRankedTeam(PacketHeader InHeader, Connection InConnection, RankingVisual InData)
		{
			ResultsSema.WaitOne();
			RecievedRankingVisuals.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveAddWaitingTeam(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			RecievedWaitingTeams.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveHatNames(PacketHeader InHeader, Connection InConnection, HatTeamNetData InData)
		{
			ResultsSema.WaitOne();
			RecievedHatNames = InData;
			ResultsSema.Release();
		}

		static void RecieveStartWheelSpin(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ResultsSema.WaitOne();
			bRecievedStartWheelSpin = true;
			ResultsSema.Release();
		}

		void ClientPingServer()
		{
			try
			{
				UDPConnection.SendObject("C2SPing", UDPConnection.ExistingLocalListenEndPoints()[0].Port, new IPEndPoint(IPAddress.Broadcast, 20000));
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		void ServerAquireConnectionInfo()
		{
			foreach (System.Net.IPEndPoint localEndPoint in TCPConnection.ExistingLocalListenEndPoints())
			{
				if (!IPAddress.IsLoopback(localEndPoint.Address) && localEndPoint.Address.ToString().IndexOf(':') == -1)
				{
					ServerConnectionInfo = new ConnectionInfo(localEndPoint.Address.ToString(), 10000);
					break;
				}
			}
		}

		static void RecieveClientConnected(PacketHeader InHeader, Connection InConnection, string InData)
		{
			RecentConnectedListSema.WaitOne();
			RecentConnectedClients.Add(InConnection.ConnectionInfo);
			RecentConnectedListSema.Release();

			ConnectionInfoToJudgeIdMap.Add(InConnection.ConnectionInfo, InData);
		}

		static void RecieveClientPing(PacketHeader InHeader, Connection InConnection, int InData)
		{
            UDPConnection.SendObject("S2CPing", new ClientConnectInfoNetData(ServerConnectionInfo.RemoteEndPoint.Address.ToString(), RoutineMinutesLength), new IPEndPoint(IPAddress.Broadcast, InData));
		}

		static void RecieveCountResult(PacketHeader InHeader, Connection InConnection, CounterData InData)
		{
			if (!bIsScoreboard)
			{
				SendToScoreboardsNetData("CountResult", InData);
			}

			ResultsSema.WaitOne();
			MainWindow.RecievedCounterData.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveDiffResult(PacketHeader InHeader, Connection InConnection, DiffNetData InData)
		{
			if (!RecievedDiffData.ContainsKey(InData.JudgeId))
			{
				RecievedDiffData.Add(InData.JudgeId, new List<DiffNetData>());
			}

			RecievedDiffData[InData.JudgeId].Add(InData);

			if (!RecievedDiffDataLastSentToScoreboard.ContainsKey(InData.JudgeId))
			{
				RecievedDiffDataLastSentToScoreboard[InData.JudgeId] = 0;
			}
		}

		static void RecieveMoveResult(PacketHeader InHeader, Connection InConnection, MoveNetData InData)
		{
			ResultsSema.WaitOne();
			MainWindow.RecievedMoveData.Add(InData);
			ResultsSema.Release();

			if (!bIsScoreboard)
			{
				SendToScoreboardsNetData("MoveResult", InData);
			}
		}

		static void RecieveAiResult(PacketHeader InHeader, Connection InConnection, string InData)
		{
			if (!bIsScoreboard)
			{
				SendToScoreboardsNetData("AiResult", InData);
			}

			ResultsSema.WaitOne();
			MainWindow.RecievedAiData.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveMusicResult(PacketHeader InHeader, Connection InConnection, float InData)
		{
			if (!bIsScoreboard)
			{
				SendToScoreboardsNetData("MusicResult", InData);
			}

			RecievedMusicScore = InData;
		}

		static void RecieveClientFinished(PacketHeader InHeader, Connection InConnection, string InData)
		{
			FinishedJudgerFlags |= InData == "Judge1" ? 1 : 0;
			FinishedJudgerFlags |= InData == "Judge2" ? 2 : 0;
			FinishedJudgerFlags |= InData == "Judge3" ? 4 : 0;
		}

		static void RecieveBackupCount(PacketHeader InHeader, Connection InConnection, JudgeCountData InData)
		{
			ResultsSema.WaitOne();
			InData.ClientConnectionInfo = InConnection.ConnectionInfo;
			RecievedBackupCount.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveBackupDiff(PacketHeader InHeader, Connection InConnection, JudgeDiffData InData)
		{
			ResultsSema.WaitOne();
			InData.ClientConnectionInfo = InConnection.ConnectionInfo;
			RecievedBackupDiff.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveBackupAi(PacketHeader InHeader, Connection InConnection, JudgeAiData InData)
		{
			ResultsSema.WaitOne();
			InData.ClientConnectionInfo = InConnection.ConnectionInfo;
			RecievedBackupAi.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveBackupMusic(PacketHeader InHeader, Connection InConnection, JudgeMusicData InData)
		{
			ResultsSema.WaitOne();
			InData.ClientConnectionInfo = InConnection.ConnectionInfo;
			RecievedBackupMusic.Add(InData);
			ResultsSema.Release();
		}

		static void RecieveBackupTwoPoint(PacketHeader InHeader, Connection InConnection, TwoPointBackupData InData)
		{
			ResultsSema.WaitOne();

			InData.UnPackData();
			TwoPointBackupServerData = InData;

			ResultsSema.Release();
		}

		static void RecieveScoreboardConnected(PacketHeader InHeader, Connection InConnection, string InData)
		{
			ScoreboardConnections.Add(new ScoreboardConnectionInfo(InConnection.ConnectionInfo));
		}

		public void ServerTickElapsed(Object sender, ElapsedEventArgs e)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Threading.ThreadStart(() =>
			{
				ServerTick();
			}));
		}

		public static float GetMoveDiff(List<DiffNetData> InDiffData, double InRoutineTime)
		{
			int DiffIndex = 0;

			while (DiffIndex < InDiffData.Count)
			{
				if (InRoutineTime - InDiffData[DiffIndex].RoutineTime <= DiffWindowBefore)
					break;

				++DiffIndex;
			}

			float Ret = 0;
			int StartDiffIndex = DiffIndex;
			for (int i = 0; i < 24 && (StartDiffIndex + i) < InDiffData.Count; ++i)
			{
				DiffNetData Data = InDiffData[StartDiffIndex + i];
				double TimeDelta = Data.RoutineTime - InRoutineTime;
				if (TimeDelta < DiffWindowAfter)
				{
					Ret = Math.Max(Ret, Data.DiffScore);

					DiffIndex = StartDiffIndex + i;
				}
				else
					break;
			}

			return Ret;
		}

		public float GetMoveDiff(double InRoutineTime)
		{
			return 0f; // GetMoveDiff(RecievedDiffData, InRoutineTime);
		}

		public float GetMoveDiff(MoveNetData InData)
		{
			return GetMoveDiff(InData.RoutineTime);
		}

		public void ClientTickElapsed(Object sender, ElapsedEventArgs e)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Threading.ThreadStart(() =>
			{
				ClientTick();
			}));
		}

		public void ScoreboardTickElapsed(Object sender, ElapsedEventArgs e)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Threading.ThreadStart(() =>
			{
				ScoreboardTick();
			}));
		}

		public Pen GetFinishPen(EFinish InFinish)
		{
			switch (InFinish)
			{
				case EFinish.Catch:
					return CatchPen;
				case EFinish.Bobble:
					return BobblePen;
				case EFinish.Drop:
					return DropPen;
			}

			return MovePen;
		}

		public void UpdateReconnectToServer()
		{
			if (!PingServerTimer.Enabled)
			{
				bool bConnectedToServer = false;
				foreach (ConnectionInfo ci in NetworkComms.AllConnectionInfo())
				{
					if (ci.ConnectionType == ConnectionType.TCP)
					{
						bConnectedToServer = true;
						break;
					}
				}

				if (!bConnectedToServer)
				{
					PingServerTimer.Start();

					NetStatusStr = "Connecting";
					NetStatusColor = Brushes.LightYellow;
				}
			}
		}

		public void SendRankingsToScoreboard(ScoreboardConnectionInfo InScorboardConnection)
		{
			if (InScorboardConnection.bIsConnected)
			{
				SendToConnectionNetData("InitRankings", InScorboardConnection.ScoreboardConnection, "");
				int TeamRank = 1;
				foreach (RoutineScore rs in SortedResults)
				{
					RankingVisual VisualData = new RankingVisual(TeamRank++ + ". " + rs.TeamName,
						rs.Judge1Score, rs.Judge2Score, rs.Judge3Score, rs.TotalScore - SortedResults[0].TotalScore);
					SendToConnectionNetData("AddRankedTeam", InScorboardConnection.ScoreboardConnection, VisualData);
				}

				foreach (RoutineScore rs in PoolScores.AllRoutineScores)
				{
					if (!rs.bPlayed)
						SendToConnectionNetData("AddWaitingTeam", InScorboardConnection.ScoreboardConnection, rs.TeamName);
				}
			}
		}

		public void SendRankingsToAllScoreboards()
		{
			foreach (ScoreboardConnectionInfo sci in ScoreboardConnections)
			{
				SendRankingsToScoreboard(sci);
			}
		}

		public void ClientTick()
		{
			UpdateReconnectToServer();

			if (NetExceptionTimer > 0)
			{
				NetExceptionTimer -= TickTimer.Interval / 1000.0;

				if (NetStatusColor != Brushes.LightPink)
					SavedNetStatusColor = NetStatusColor;

				NetStatusColor = Brushes.LightPink;
			}
			else if (NetStatusColor == Brushes.LightPink)
				NetStatusColor = SavedNetStatusColor;

			if (bInitScores)
			{
				bInitScores = false;

				ResetJudgers();

				TwoPointStartRoutine();
			}

			SendBackupCooldownTimer -= (float)TickTimer.Interval / 1000f;
			if (!bRoutineRecording && !bWaitingResults && bIsClientConnected && SendBackupCooldownTimer < 0)
			{
				BackupData.SendAllWaitingBackups();
			}

			if (JudgersTabControl.SelectedIndex == 2)
			{
				DiffUpdateTick((float)(TickTimer.Interval / 1000f));
			}
			else
				DisplayDiffScore = "None";

			if (JudgersTabControl.SelectedIndex == 4 && bRoutineRecording)
			{
				MusicScoreCooldown -= (float)TickTimer.Interval / 1000f;

				if (LastMusicScore != MusicSlider0.Value && MusicScoreCooldown < 0)
				{
					MusicScoreCooldown = .5f;
					LastMusicScore = MusicSlider0.Value;

					CurJudgeMusicData.MusicScore = (float)MusicSlider0.Value;

					if (bIsClientConnected)
						SendToServerNetData("MusicResult", (float)MusicSlider0.Value);
				}
			}

			if (bWaitingResults)
			{
				SetFinishedButtons(EJudgerState.Finishing);
			}
			else if (bRoutineRecording)
			{
				SetFinishedButtons(EJudgerState.Judging);
			}
			else
			{
				SetFinishedButtons(EJudgerState.Ready);
			}

			SetJudgeInterfaceEnabled(!bWaitingForReady || bWaitingResults || bRoutineRecording);

			if (RecievedTeamInfo != JudgeRoutineText)
			{
				JudgeRoutineText = RecievedTeamInfo;
			}

			NotifyPropertyChanged("TimeRemainingText");

			ResultsSema.WaitOne();

			bool bBackupDirty = false;
			bBackupDirty |= MarkTransfered(ref BackupData.BackupCountData, ref RecievedConfirmBackupCount);
			bBackupDirty |= MarkTransfered(ref BackupData.BackupDiffData, ref RecievedConfirmBackupDiff);
			bBackupDirty |= MarkTransfered(ref BackupData.BackupAiData, ref RecievedConfirmBackupAi);
			bBackupDirty |= MarkTransfered(ref BackupData.BackupMusicData, ref RecievedConfirmBackupMusic);

			if (bBackupDirty)
				WriteBackupToDisk();

			ResultsSema.Release();
		}

		public void ServerTick()
		{
			if (ServerConnectionInfo == null)
				ServerAquireConnectionInfo();

			int ConCount = 0;
			foreach (ConnectionInfo ci in NetworkComms.AllConnectionInfo())
			{
				if (ci.ConnectionType == ConnectionType.TCP)
				{
					bool bIsScoreboardConnection = false;
					foreach (ScoreboardConnectionInfo sci in ScoreboardConnections)
					{
						if (sci.ScoreboardConnection == ci)
						{
							bIsScoreboardConnection = true;
							break;
						}
					}

					if (!bIsScoreboardConnection)
						++ConCount;
				}
			}

			int duplicateJudgeFlags = 0;
			int connectedJudgeFlags = 0;
			List<string> foundJudgeIds = new List<string>();
			foreach (var connectionInfo in ConnectionInfoToJudgeIdMap.Keys)
			{
				if (NetworkComms.AllConnectionInfo().Contains(connectionInfo))
				{
					string judgeId = ConnectionInfoToJudgeIdMap[connectionInfo];
					if (foundJudgeIds.Contains(judgeId))
					{
						duplicateJudgeFlags |= judgeId == "Judge1" ? 1 : 0;
						duplicateJudgeFlags |= judgeId == "Judge2" ? 2 : 0;
						duplicateJudgeFlags |= judgeId == "Judge3" ? 4 : 0;
					}
					else
					{
						foundJudgeIds.Add(judgeId);

						connectedJudgeFlags |= judgeId == "Judge1" ? 1 : 0;
						connectedJudgeFlags |= judgeId == "Judge2" ? 2 : 0;
						connectedJudgeFlags |= judgeId == "Judge3" ? 4 : 0;
					}
				}
			}

			ProcessTwoPointBackup();

			if (ConCount == 3 && ServerConnectionInfo != null)
				SelfObj.NetStatusColor = Brushes.LightGreen;
			else
				SelfObj.NetStatusColor = Brushes.LightSalmon;

			if (ServerConnectionInfo == null || ServerConnectionInfo.RemoteEndPoint == null)
				SelfObj.NetStatusStr = "Not Listening";
			else
				SelfObj.NetStatusStr = ServerConnectionInfo.RemoteEndPoint.Address.ToString() + ":" + ServerConnectionInfo.RemoteEndPoint.Port + " |";

			SelfObj.NetStatusStr += " Connections: " + ConCount;

			if (bWaitingResults)
			{
                if ((bAnyFinished && FinishedJudgerFlags > 0) ||
                    (bOnlyConnectedFinished && connectedJudgeFlags == FinishedJudgerFlags) ||
                    FinishedJudgerFlags == 7)
                {
                    FinishRoutineServer();
                }
			}
			else
				FinishedJudgerFlags = 0;

			Judge1Bg = (connectedJudgeFlags & 1) != 0 ? Brushes.LightSkyBlue : Brushes.White;
			Judge2Bg = (connectedJudgeFlags & 2) != 0 ? Brushes.LightSkyBlue : Brushes.White;
			Judge3Bg = (connectedJudgeFlags & 4) != 0 ? Brushes.LightSkyBlue : Brushes.White;

			Judge1Bg = (FinishedJudgerFlags & 1) != 0 ? Brushes.LightGreen : Judge1Bg;
			Judge2Bg = (FinishedJudgerFlags & 2) != 0 ? Brushes.LightGreen : Judge2Bg;
			Judge3Bg = (FinishedJudgerFlags & 4) != 0 ? Brushes.LightGreen : Judge3Bg;

			Judge1Bg = (duplicateJudgeFlags & 1) != 0 ? Brushes.LightSalmon : Judge1Bg;
			Judge2Bg = (duplicateJudgeFlags & 2) != 0 ? Brushes.LightSalmon : Judge2Bg;
			Judge3Bg = (duplicateJudgeFlags & 4) != 0 ? Brushes.LightSalmon : Judge3Bg;

			RecentConnectedListSema.WaitOne();
			foreach (ConnectionInfo ci in RecentConnectedClients)
			{
				if (ci != null && ci.RemoteEndPoint != null)
					SendToConnectionNetData("TeamInfo", ci, CurrentRoutineIndex >= 0 ? RoutineButtonText : "Waiting");
			}

			RecentConnectedClients.Clear();
			RecentConnectedListSema.Release();

			foreach (ScoreboardConnectionInfo sci in ScoreboardConnections)
			{
				if (!sci.bInited)
				{
					SendToConnectionNetData("TeamInfo", sci.ScoreboardConnection, CurrentRoutineIndex >= 0 ? RoutineButtonText : "Waiting");

					foreach (RoutineScore rs in PoolScores.AllRoutineScores)
					{
						if (LeaderScore == null || rs.TotalScore > LeaderScore.TotalScore)
							LeaderScore = rs;
					}

					if (LeaderScore != null)
					{
						SendToConnectionNetData("LeaderData", sci.ScoreboardConnection,
							new LeaderNetData(LeaderScore.TeamName, LeaderScore.TotalScore));
					}

					SendRankingsToScoreboard(sci);

					sci.bInited = true;
				}
			}

			ResultsSema.WaitOne();

			// Update output
			string NewOutputText = "Time: " + SecondsIntoRoutine.ToString("0.0") + "\r\n\r\n";

			int PlaceIndex = 1;
			foreach (RoutineScore rs in SortedResults)
			{
				NewOutputText += PlaceIndex++ + ". " + rs.TeamNameTrim + " - " +
					rs.Judge1Score.ToString("0.00") + ", " + rs.Judge2Score.ToString("0.00") + ", " + rs.Judge3Score.ToString("0.00") +
					"  Total: " + rs.TotalScore + "\r\n";
			}

			#region Debug Output
			//ResultsTextBox.Text += "Music: " + RecievedMusicScore + "\r\n\r\n";
			//for (int i = 0; i < AiScores.Length; ++i)
			//{
			//    ResultsTextBox.Text += AiNames[i] + ": " + AiScores[i] + "\r\n";
			//}

			//ResultsTextBox.Text += "\r\nCurrent Combo:\r\n";
			//foreach (MoveScore ms in CurComboScore.ComboPointsList)
			//{
			//    ResultsTextBox.Text += ms.MovePoints + "  " + ms.MoveData.RoutineTime.ToString() + "\r\n";
			//}
			//ResultsTextBox.Text += "Total: " + CurComboScore.GetTotalPoints() + "\r\n";

			//ResultsTextBox.Text += "\r\n\r\n";
			//for (int i = 0; i < CurRoutineComboScores.Count; ++i)
			//{
			//    foreach (MoveScore ms in CurRoutineComboScores[i].ComboPointsList)
			//    {
			//        ResultsTextBox.Text += ms.MovePoints + "  " + ms.MoveData.RoutineTime.ToString() + "\r\n";
			//    }

			//    ResultsTextBox.Text += "Total: " + CurRoutineComboScores[i].GetTotalPoints() + "\r\n\r\n";
			//}
			#endregion

			if (ResultsTextBox.Text != NewOutputText)
				ResultsTextBox.Text = NewOutputText;

			// Update scoreboard
			foreach (ScoreboardConnectionInfo sci in ScoreboardConnections)
			{
				foreach (var diffData in RecievedDiffData)
				{
					int lastSentDiffIndex = RecievedDiffDataLastSentToScoreboard[diffData.Key];
					while (lastSentDiffIndex + 1 < diffData.Value.Count)
					{
						if (sci.bIsConnected)
							SendToConnectionNetData("ScoreboardDiffResult", sci.ScoreboardConnection, diffData.Value[++lastSentDiffIndex]);
						else
							break;
					}

					RecievedDiffDataLastSentToScoreboard[diffData.Key] = lastSentDiffIndex;
				}
			}

			double Interval = SecondsIntoRoutine / SplitIntervalTime;
			if (bRoutineRecording && ((SplitInterval + 1) * SplitIntervalTime) <= (RoutineMinutesLength * 60 + .1) && Interval > 0 && ((int)Interval) > SplitInterval)
			{
				SplitInterval = (int)Interval;

				CurSplit.Judge1Score = CalcJudgeTwoPointScore("Judge1");
				CurSplit.Judge2Score = CalcJudgeTwoPointScore("Judge2");
				CurSplit.Judge3Score = CalcJudgeTwoPointScore("Judge3");

				SplitNetData snd = new SplitNetData();
				snd.Current = CurSplit;
				if (LeaderScore != null && SplitInterval < LeaderScore.Splits.Count)
					snd.Leader = LeaderScore.Splits[SplitInterval];

				SendToScoreboardsNetData("SplitData", snd);

				CurSplits.Add(CurSplit.Duplicate());
			}

			// Handle backups
			bool bBackupDirty = false;
			bBackupDirty |= AddToBackup(ref BackupData.BackupCountData, ref RecievedBackupCount);
			bBackupDirty |= AddToBackup(ref BackupData.BackupDiffData, ref RecievedBackupDiff);
			bBackupDirty |= AddToBackup(ref BackupData.BackupAiData, ref RecievedBackupAi);
			bBackupDirty |= AddToBackup(ref BackupData.BackupMusicData, ref RecievedBackupMusic);

			if (bBackupDirty)
			{
				WriteBackupToDisk();

				BackupData.CalcCompletedRoutines();

				RebuildBackupDisplayList();
			}

			ResultsSema.Release();
		}

		public void ScoreboardTick()
		{
			try
			{
				NotifyPropertyChanged("ScoreboardTimeText");

				UpdateReconnectToServer();

				if (RecievedToggleScoreboardRandom >= 0)
				{
					JudgersTabControl.SelectedIndex = RecievedToggleScoreboardRandom == 1 ? 8 : 6;

					RecievedToggleScoreboardRandom = -1;
				}

				if (RecievedToggleScoreboard >= 0)
				{
					JudgersTabControl.SelectedIndex = RecievedToggleScoreboard == 1 ? 7 : 6;

					RecievedToggleScoreboard = -1;
				}

				if (JudgersTabControl.SelectedIndex == 8)
					TickTimer.Interval = 30;
				else
					TickTimer.Interval = 150;

				// scoreboard finished
				if (bRoutineRecording && SecondsIntoRoutine >= RoutineMinutesLength * 60f)
				{
					bRoutineRecording = false;

					if (CalcAllJudgesTwoPointScore() > CalcAllJudgesTwoPointScore(ScoreboardLeaderRecievedDiffData))
					{
						ScoreboardLeaderRecievedDiffData = new Dictionary<string, List<DiffNetData>>();
						foreach (string key in RecievedDiffData.Keys)
						{
							ScoreboardLeaderRecievedDiffData.Add(key, RecievedDiffData[key]);
						}

						LeaderTeamText = CurrentTeamText;
						Judge1LeaderScore = CalcJudgeTwoPointScore("Judge1");
						Judge2LeaderScore = CalcJudgeTwoPointScore("Judge2");
						Judge3LeaderScore = CalcJudgeTwoPointScore("Judge3");
					}

					Judge1Score = CalcJudgeTwoPointScore("Judge1");
					Judge2Score = CalcJudgeTwoPointScore("Judge2");
					Judge3Score = CalcJudgeTwoPointScore("Judge3");
				}

				// scoreboard start
				if (bScoreboardRoutineStart)
				{
					bScoreboardRoutineStart = false;

					CurrentTeamText = NextTeamName;

					MaxDiffHeight = 10f;

					bInitScores = true;
				}

				if (bInitScores)
				{
					bInitScores = false;

					ResetSeverScores();
				}

				if (bInitRankings)
				{
					bInitRankings = false;

					ScorboardRankings.Clear();
					ScorboardToPlay.Clear();
				}

				ResultsSema.WaitOne();

				#region RecieveStuff
				if (RecievedLeaderData != null)
				{
					LeaderTeamText = RecievedLeaderData.TeamName;
					//LeaderDiff = RecievedLeaderData.DiffScore;
					//LeaderAi = RecievedLeaderData.AiScore;
					//LeaderMusic = RecievedLeaderData.MusicScore;

					RecievedLeaderData = null;
				}

				if (RecievedTeamInfo != null && !RecievedTeamInfo.StartsWith("Waiting"))
				{
					NextTeamName = RecievedTeamInfo;

					RecievedTeamInfo = null;
				}

				if (RecievedCurrentDiffScore >= 0)
				{
					CurrentDiff = RecievedCurrentDiffScore;

					RecievedCurrentDiffScore = -1;
				}

				if (RecievedCurrentAiScore >= 0)
				{
					CurrentAi = RecievedCurrentAiScore;

					RecievedCurrentAiScore = -1;
				}

				if (RecievedMusicScore >= 0)
				{
					CurrentMusic = RecievedMusicScore * MusicPointsMulti;

					RecievedMusicScore = -1;
				}

				if (RecievedSplitData != null)
				{
					CurrentSplitJudge1 = RecievedSplitData.Current.Judge1Score;
					CurrentSplitJudge2 = RecievedSplitData.Current.Judge2Score;
					CurrentSplitJudge3 = RecievedSplitData.Current.Judge3Score;

					if (RecievedSplitData.Leader != null)
					{
						LeaderSplitJudge1 = RecievedSplitData.Leader.Judge1Score;
						LeaderSplitJudge2 = RecievedSplitData.Leader.Judge2Score;
						LeaderSplitJudge3 = RecievedSplitData.Leader.Judge3Score;
					}
					else
					{
						LeaderSplitJudge1 = RecievedSplitData.Current.Judge1Score;
						LeaderSplitJudge2 = RecievedSplitData.Current.Judge2Score;
						LeaderSplitJudge3 = RecievedSplitData.Current.Judge3Score;
					}

					RecievedSplitData = null;
				}

				/////////////////// Hat teams
				if (RecievedHatNames != null)
				{
					WheelNames = new List<string>(RecievedHatNames.HatNames);
					RecievedHatNames = null;

					GenerateRandomWheelNames();
				}

				if (bRecievedStartWheelSpin)
				{
					bRecievedStartWheelSpin = false;

					StartPickingTeams();
				}
				#endregion


				/////////////////// Judge multipliers
				Judge1Multiplier = GetLastTwoPointMutliplier("Judge1");
				Judge2Multiplier = GetLastTwoPointMutliplier("Judge2");
				Judge3Multiplier = GetLastTwoPointMutliplier("Judge3");

				/////////////////// Diff graph
				if (JudgersTabControl.SelectedIndex == 6)
				{
					#region Diff Graph Drawing
					double CanvasWidth = DiffGraphCanvas.ActualWidth;
					double CanvasHeight = DiffGraphCanvas.ActualHeight - 20;

					// Resize the bmp if needed
					if ((CanvasWidth > 100 && CanvasHeight > 100) && (DiffGraphBitmap == null ||
						(((int)DiffGraphBitmap.Width != (int)CanvasWidth || (int)DiffGraphBitmap.Height != (int)CanvasHeight))))
						DiffGraphBitmap = new RenderTargetBitmap((int)CanvasWidth, (int)CanvasHeight, 96, 96, PixelFormats.Pbgra32);

					if (DiffGraphBitmap != null)
					{
						DiffGraphBitmap.Clear();
						DrawingContext DiffGraphContext = DiffGraphVisual.RenderOpen();

						DiffGraphContext.DrawRectangle(Brushes.LightGray, DiffGraphBgPen, new Rect(0, 0, CanvasWidth, CanvasHeight));

						float DiffLineWindowSeconds = 25f;
						double BgX = 0;
						if (bRoutineRecording)
						{
							double t = SecondsIntoRoutine / DiffLineWindowSeconds - ((int)(SecondsIntoRoutine / DiffLineWindowSeconds));
							BgX = -t * CanvasWidth;

							DiffGraphContext.DrawImage(DiffGraphBg, new Rect(BgX + CanvasWidth, 0, CanvasWidth, CanvasHeight));
						}

						DiffGraphContext.DrawImage(DiffGraphBg, new Rect(BgX, 0, CanvasWidth, CanvasHeight));


						double StartTime = Math.Max(0.0, SecondsIntoRoutine - DiffLineWindowSeconds);

						MaxDiffHeight = Math.Max(CalcAllJudgesTwoPointScore((float)RoutineMinutesLength * 60f), MaxDiffHeight);
						MaxDiffHeight = Math.Max(CalcAllJudgesTwoPointScore(ScoreboardLeaderRecievedDiffData, (float)SecondsIntoRoutine), MaxDiffHeight);

						const float DrawTimeInterval = 1f;
						float DrawTime = DrawTimeInterval;
						while (DrawTime < SecondsIntoRoutine)
						{
							float DrawTimeScore = CalcAllJudgesTwoPointScore(DrawTime);
							float PrevDrawTimeScore = CalcAllJudgesTwoPointScore(DrawTime - DrawTimeInterval);

							if (DrawTime >= StartTime)
							{
								if (ScoreboardLeaderRecievedDiffData.Count > 0)
								{
									float LeaderDrawTimeScore = CalcAllJudgesTwoPointScore(ScoreboardLeaderRecievedDiffData, DrawTime);
									float LeaderPrevDrawTimeScore = CalcAllJudgesTwoPointScore(ScoreboardLeaderRecievedDiffData, DrawTime - DrawTimeInterval);

									DiffGraphContext.DrawLine(LeaderTwoPointScorePen,
										new Point((1f - (SecondsIntoRoutine - DrawTime) / DiffLineWindowSeconds) * CanvasWidth,
										(1 - LeaderPrevDrawTimeScore / MaxDiffHeight) * CanvasHeight + 10),
										new Point((1f - (SecondsIntoRoutine - DrawTime - DrawTimeInterval) / DiffLineWindowSeconds) * CanvasWidth,
										(1 - LeaderDrawTimeScore / MaxDiffHeight) * CanvasHeight + 10));
								}

								DiffGraphContext.DrawLine(DiffLinePen,
									new Point((1f - (SecondsIntoRoutine - DrawTime) / DiffLineWindowSeconds) * CanvasWidth,
									(1 - PrevDrawTimeScore / MaxDiffHeight) * CanvasHeight + 10),
									new Point((1f - (SecondsIntoRoutine - DrawTime - DrawTimeInterval) / DiffLineWindowSeconds) * CanvasWidth,
									(1 - DrawTimeScore / MaxDiffHeight) * CanvasHeight + 10));
							}

							DrawTime += DrawTimeInterval;
						}

						double FontScale = CanvasHeight / 16.0;
						FormattedText highScoreText = new FormattedText(MaxDiffHeight.ToString("0.0"),
							CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface(new FontFamily("Verdana"),
							FontStyles.Normal, FontWeights.UltraBold, FontStretches.Normal), FontScale, DiffGraphScorePen.Brush);
						DiffGraphContext.DrawText(highScoreText, new Point(CanvasWidth - highScoreText.Width - 5, 5));

						FormattedText midScoreText = new FormattedText((MaxDiffHeight / 2f).ToString("0.0"),
							CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface(new FontFamily("Verdana"),
							FontStyles.Normal, FontWeights.UltraBold, FontStretches.Normal), FontScale, DiffGraphScorePen.Brush);
						DiffGraphContext.DrawText(midScoreText, new Point(CanvasWidth - midScoreText.Width - 5, CanvasHeight / 2 - midScoreText.Height / 2));

						DiffGraphContext.Close();

						DiffGraphBitmap.Render(DiffGraphVisual);

						DiffGraphCanvas.Background = new ImageBrush(DiffGraphBitmap);
					}
					#endregion
				}
				else if (JudgersTabControl.SelectedIndex == 8 && RandomNames.Count > 0)
				{
					double CanvasWidth = WheelCanvas.ActualWidth;
					double CanvasHeight = WheelCanvas.ActualHeight - 20;

					double WheelSpeed = 0;
					if (TargetWheelDistance > 0)
						WheelSpeed = 6 * Math.Max(.05, Math.Min(1.0, TargetWheelDistance / 150));
					
					WheelRotation += WheelSpeed;

					TargetWheelDistance -= WheelSpeed;
					if (TargetWheelDistance < 0)
					{
						TargetWheelDistance = 0;

						AddHatName(RandomNames[WheelEndIndex]);

						++CurHatTeamMemberCount;

						if (CurHatTeamMemberCount >= 3)
						{
							if (RandomNames.Count != 6)
							{
								CurHatTeamMemberCount = 0;
								bHatTeamAltBool = !bHatTeamAltBool;

								AddTeamHeader();
							}
						}

						EndWheelSpin();
					}

					if ((CanvasWidth > 100 && CanvasHeight > 100) && (WheelBitmap == null ||
						(((int)WheelBitmap.Width != (int)CanvasWidth || (int)WheelBitmap.Height != (int)CanvasHeight))))
						WheelBitmap = new RenderTargetBitmap((int)CanvasWidth, (int)CanvasHeight, 96, 96, PixelFormats.Pbgra32);

					if (WheelBitmap != null)
					{
						WheelBitmap.Clear();
						DrawingContext WheelContext = WheelVisual.RenderOpen();

						double Radius = CanvasHeight * .95 / 2;
						DrawWheelNames(WheelContext, Radius + 20, Radius + CanvasHeight * .025, Radius, WheelRotation);

						WheelContext.Close();

						WheelBitmap.Render(WheelVisual);

						WheelCanvas.Background = new ImageBrush(WheelBitmap);
					}
				}

				/////////////////////// Rankings
				foreach (RankingVisual rv in RecievedRankingVisuals)
					ScorboardRankings.Add(rv);
				RecievedRankingVisuals.Clear();

				foreach (string s in RecievedWaitingTeams)
					ScorboardToPlay.Add(s);
				RecievedWaitingTeams.Clear();

				ResultsSema.Release();
			}
			catch
			{
				ResultsSema.Release();
			}
		}

		public void ProcessTwoPointBackup()
		{
			if (TwoPointBackupServerData != null)
			{
				foreach (string key in TwoPointBackupServerData.Data.Keys)
				{
					if (!RecievedDiffData.ContainsKey(TwoPointBackupServerData.JudgeId))
					{
						RecievedDiffData.Add(TwoPointBackupServerData.JudgeId, new List<DiffNetData>());
					}

					List<DiffNetData> diffList = RecievedDiffData[TwoPointBackupServerData.JudgeId];
					diffList.Clear();

					foreach (DiffNetData dnd in TwoPointBackupServerData.Data[key].Data)
					{
						diffList.Add(dnd);
					}

					RoutineScore NewScore = new RoutineScore(
						CalcJudgeTwoPointScore("Judge1"), CalcJudgeTwoPointScore("Judge2"), CalcJudgeTwoPointScore("Judge3"), CurSplits, true);
					
					string teamName = key.Substring(key.IndexOf(". ") + 2);
					NewScore.TeamName = teamName;

					bool bFoundTeam = false;
					for (int i = 0; i < PoolScores.AllRoutineScores.Count; ++i)
					{
						if (PoolScores.AllRoutineScores[i].TeamName == teamName)
						{
							bFoundTeam = true;

							PoolScores.AllRoutineScores[i] = NewScore;
							UpdateSortedResults(PoolScores.AllRoutineScores[i]);
							break;
						}
					}

					if (!bFoundTeam)
					{
						PoolScores.AllRoutineScores.Add(NewScore);
						UpdateSortedResults(PoolScores.AllRoutineScores.Last());
					}
				}

				bResultsDirty = true;

				ResetRoutine(false);

				ResetSeverScores();

				SendRankingsToAllScoreboards();

				SavePoolData();

				TwoPointBackupServerData = null;
			}
		}

		public void AddHatName(string InName)
		{
			TextBlock NewName = new TextBlock();
			NewName.FontSize = 37;
			NewName.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
			NewName.Background = bHatTeamAltBool ? Brushes.LightSteelBlue : Brushes.LightGray;
			NewName.Text = InName;

			if (CurTeamNumber < 8)
				HatTeamsStack.Children.Add(NewName);
			else
				HatTeamsStack2.Children.Add(NewName);
		}

		public void AddTeamHeader()
		{
			TextBlock NewTeam = new TextBlock();
			NewTeam.FontSize = 20;
			NewTeam.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
			NewTeam.Background = Brushes.BurlyWood;
			NewTeam.Text = "Team " + CurTeamNumber + ":";
			++CurTeamNumber;
			if (CurTeamNumber < 8)
				HatTeamsStack.Children.Add(NewTeam);
			else
				HatTeamsStack2.Children.Add(NewTeam);
		}

		public void EndWheelSpin()
		{
			WheelDataSema.WaitOne();
			if (RandomNames.Count <= 5 && CurHatTeamMemberCount == 0)
			{
				RandomNames.RemoveAt(WheelEndIndex);

				foreach (string name in RandomNames)
					AddHatName(name);

				RandomNames.Clear();
			}
			else
			{
				NextSpinTimer.Start();
			}
			WheelDataSema.Release();
		}

		void NextSpin_Elapsed(object sender, ElapsedEventArgs e)
		{
			SpinWheel();
		}

		public void SpinWheel()
		{
			WheelDataSema.WaitOne();
			if (WheelEndIndex != -1 && RandomNames.Count > WheelEndIndex)
				RandomNames.RemoveAt(WheelEndIndex);

			int EndIndex = RandomNames.IndexOf(WheelNames[SpinIndex++]);

			WheelEndIndex = EndIndex;
			int NamesCount = RandomNames.Count;
			double SliceAngle = 360.0 / NamesCount;
			double TargetAngle = (NamesCount - EndIndex) * SliceAngle;
			TargetWheelDistance = TargetAngle - (WheelRotation % 360.0);
			if (TargetWheelDistance < 0)
				TargetWheelDistance += 360;

			TargetWheelDistance += 360;
			WheelDataSema.Release();
		}

		public void DrawWheelNames(DrawingContext InContext, double CenX, double CenY, double Radius, double InRotation)
		{
			double CanvasWidth = WheelCanvas.ActualWidth;
			double CanvasHeight = WheelCanvas.ActualHeight - 20;

			int NamesCount = RandomNames.Count;
			double SliceAngle = 360.0 / NamesCount;
			double CurRot = InRotation;
			double CurRotRad = CurRot / 180.0 * Math.PI;
			double SliceAngleRad = SliceAngle / 180.0 * Math.PI;
			bool AltBool = false;

			WheelDataSema.WaitOne();
			foreach (string name in RandomNames)
			{
				StreamGeometry GeoStream = new StreamGeometry();
				Point CenPoint = new Point(CenX, CenY);
				using (StreamGeometryContext GeoContext = GeoStream.Open())
				{
					GeoStream.Clear();

					double HalfSliceRot = SliceAngleRad / 2;
					double StartOffset = .35;
					Point StartLeft = new Point(Math.Cos(CurRotRad - HalfSliceRot) * Radius * StartOffset + CenPoint.X, Math.Sin(CurRotRad - HalfSliceRot) * Radius * StartOffset + CenPoint.Y);
					Point StartRight = new Point(Math.Cos(CurRotRad + HalfSliceRot) * Radius * StartOffset + CenPoint.X, Math.Sin(CurRotRad + HalfSliceRot) * Radius * StartOffset + CenPoint.Y);

					GeoContext.BeginFigure(StartLeft, true, true);

					Point EndLeft = new Point(Math.Cos(CurRotRad - HalfSliceRot) * Radius + CenPoint.X, Math.Sin(CurRotRad - HalfSliceRot) * Radius + CenPoint.Y);
					Point EndRight = new Point(Math.Cos(CurRotRad + HalfSliceRot) * Radius + CenPoint.X, Math.Sin(CurRotRad + HalfSliceRot) * Radius + CenPoint.Y);

					PointCollection points = new PointCollection
					{
						EndLeft,
						EndRight,
						StartRight
					};

					GeoContext.PolyLineTo(points, true, true);
				}

				Brush BgBrush = AltBool ? Brushes.Cornsilk : Brushes.LightGray;
				if (TargetWheelDistance == 0 && WheelEndIndex >= 0 && RandomNames[WheelEndIndex] == name)
					BgBrush = Brushes.LightGreen;

				InContext.DrawGeometry(BgBrush, new Pen(Brushes.Black, 1), GeoStream);

				double FontScale = Radius / 10.0;
				FormattedText NameText = new FormattedText(name, CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
					new Typeface(new FontFamily("Verdana"), FontStyles.Normal, FontWeights.UltraBold, FontStretches.Normal), FontScale, Brushes.Black);

				Transform Cen = new TranslateTransform(Radius / 2.0, -NameText.Height / 2);
				Transform Rot = new RotateTransform(CurRot);
				Transform Tran = new TranslateTransform(CenX, CenY);

				InContext.PushTransform(Tran);
				InContext.PushTransform(Rot);
				InContext.PushTransform(Cen);
				InContext.DrawText(NameText, new Point(0, 0));
				InContext.Pop();
				InContext.Pop();
				InContext.Pop();

				CurRot += SliceAngle;
				CurRotRad += SliceAngleRad;

				AltBool = !AltBool;
			}
			WheelDataSema.Release();


			// Arrow
			if (RandomNames.Count > 0)
			{
				StreamGeometry GeoStream = new StreamGeometry();
				Point CenPoint = new Point(CenX + Radius + Radius * .1, CenY);
				double ArrowRot = 0;
				double AngleMod = (CurRot - 5) % SliceAngle;
				double FrontColAngle = SliceAngle * .4;
				double BackColAngle = SliceAngle * .1;
				double EndBackColAngle = FrontColAngle + BackColAngle;
				if (AngleMod < FrontColAngle)
					ArrowRot = -45.0 * AngleMod / FrontColAngle;
				else if (AngleMod < FrontColAngle + BackColAngle)
					ArrowRot = -45.0 * (1 - (AngleMod - FrontColAngle) / BackColAngle);
				RotateTransform RotMat = new RotateTransform(ArrowRot);
				Point TipOffset = RotMat.Transform(new Point(-Radius * .15, 0));
				Point LeftOffset = RotMat.Transform(new Point(Radius * .05, Radius * .05));
				Point RightOffset = RotMat.Transform(new Point(Radius * .05, -Radius * .05));
				using (StreamGeometryContext GeoContext = GeoStream.Open())
				{
					GeoStream.Clear();

					Point Tip = new Point(TipOffset.X + CenPoint.X, TipOffset.Y + CenPoint.Y);

					GeoContext.BeginFigure(Tip, true, true);

					Point Left = new Point(LeftOffset.X + CenPoint.X, LeftOffset.Y + CenPoint.Y);
					Point Right = new Point(RightOffset.X + CenPoint.X, RightOffset.Y + CenPoint.Y);

					PointCollection points = new PointCollection
					{
						Left,
						Right
					};

					GeoContext.PolyLineTo(points, true, true);
				}

				InContext.DrawGeometry(Brushes.DarkSalmon, new Pen(Brushes.Black, 1), GeoStream);
			}
		}

		public void GenerateRandomWheelNames()
		{
			List<string> TempNames = new List<string>(WheelNames);

			Random Rand = new Random();

			RandomNames.Clear();
			while (TempNames.Count > 0)
			{
				int NameIndex = Rand.Next() % TempNames.Count;
				RandomNames.Add(TempNames[NameIndex]);
				TempNames.RemoveAt(NameIndex);
			}
		}

		public void StartPickingTeams()
		{
			GenerateRandomWheelNames();

			HatTeamsStack.Children.Clear();
			HatTeamsStack2.Children.Clear();

			CurTeamNumber = 1;
			WheelEndIndex = -1;
			WheelRotation = 0;
			CurHatTeamMemberCount = 0;
			SpinIndex = 0;
			NextSpinTimer.Stop();

			AddTeamHeader();

			SpinWheel();
		}

        private void RoutineLengthText_TextChanged(object sender, TextChangedEventArgs e)
        {
            float minutes = 0;
            if (float.TryParse(RoutineLengthText.Text, out minutes))
            {
                RoutineMinutesLength = minutes;
            }
        }
	}

#region Data Classes

	public class BackupDisplay
	{
		public RoutineScore BackupScore = null;
		public RoutineScore CurrentScore = null;

		public string TeamName { get { if (BackupScore != null) return BackupScore.TeamName; return "No team name"; } }
		public string BackupScoreText { get { if (BackupScore != null) return "Backup: " + BackupScore.TotalScore.ToString("0.00"); return "No Backup Score"; } }
		public string CurrentScoreText { get { if (CurrentScore != null && CurrentScore.TotalScore > 0) return "Current: " + CurrentScore.TotalScore.ToString("0.00"); return "No Current Score"; } }
		public string ButtonText { get { return "Replace with Backup"; } }
	}

	public class ScoreboardConnectionInfo
	{
		public ConnectionInfo ScoreboardConnection = null;
		public bool bInited = false;
		public bool bIsConnected { get { return ScoreboardConnection != null && ScoreboardConnection.ConnectionState == ConnectionState.Established; } }

		public ScoreboardConnectionInfo() { }

		public ScoreboardConnectionInfo(ConnectionInfo InConnectionInfo)
		{
			ScoreboardConnection = InConnectionInfo;
		}
	}

	[ProtoContract]
	public class RankingVisual
	{
		[ProtoMember(1)]
		public string TeamName { get; set; }
		[ProtoMember(2)]
		public float Judge1Score { get; set; }
		[ProtoMember(3)]
		public float Judge2Score { get; set; }
		[ProtoMember(4)]
		public float Judge3Score { get; set; }
		public float TotalScore { get { return Judge1Score + Judge2Score + Judge3Score; } }
		[ProtoMember(5)]
		float _Delta = 0;
		public string Delta { get { if (_Delta == 0) return "---"; return _Delta.ToString(); } }

		public RankingVisual()
		{
			Judge1Score = 0;
			Judge2Score = 0;
			Judge3Score = 0;
		}

		public RankingVisual(string InTeamName, float inJudgeScore1, float inJudgeScore2, float inJudgeScore3, float InDelta)
		{
			TeamName = InTeamName;
			Judge1Score = inJudgeScore1;
			Judge2Score = inJudgeScore2;
			Judge3Score = inJudgeScore3;
			_Delta = InDelta;
		}
	}

	[XmlRoot("PoolData")]
	public class PoolData
	{
		[XmlArray("AllRoutineScores")]
		[XmlArrayItem("RoutineScore")]
		public List<RoutineScore> AllRoutineScores { get; set; }

		public PoolData()
		{
			AllRoutineScores = new List<RoutineScore>();
		}
	}

	public class RoutineScore
	{
		public string TeamNameTrim
		{
			get
			{
				Regex NumberReg = new Regex(@"^\d+.\s");
				Match NumberMatch = NumberReg.Match(TeamName);
				if (NumberMatch.Success)
					return TeamName.Replace(NumberMatch.Value, "");

				return TeamName;
			}
		}
		public string TeamName = "Missing Team Name";
		public float Judge1Score = 0;
		public float Judge2Score = 0;
		public float Judge3Score = 0;
		public float TotalScore { get { return Judge1Score + Judge2Score + Judge3Score; } }
		public List<SplitData> Splits;
		public bool bPlayed = false;

		public RoutineScore()
		{
		}

		public RoutineScore(string InTeamName)
		{
			TeamName = InTeamName;
		}

		public RoutineScore(float inJudgeScore1, float inJudgeScore2, float inJudgeScore3, List<SplitData> InSplits, bool bInPlayed)
		{
			Judge1Score = inJudgeScore1;
			Judge2Score = inJudgeScore2;
			Judge3Score = inJudgeScore3;

			bPlayed = bInPlayed;
			Splits = InSplits;
		}
	}

	public class DiffScoreData
	{
		public List<int> DiffScores = new List<int>();
		public EFinish FinishState = EFinish.None;
		public Button ScoreButton = null;
		public bool bEditing = false;

		public int TotalDiff
		{
			get
			{
				int Total = 0;
				foreach (int i in DiffScores)
					Total += i;

				return Total;
			}
		}

		public override string ToString()
		{
			string Ret = "";
			foreach (int i in DiffScores)
				Ret += i + " ";

			if (FinishState != EFinish.None)
				Ret += FinishState;

			return Ret;
		}
	}

	public enum EFinish
	{
		None,
		Catch,
		Drop,
		Bobble
	}

	[ProtoContract]
	public class SplitNetData
	{
		[ProtoMember(1)]
		public SplitData Leader;
		[ProtoMember(2)]
		public SplitData Current;
	}

	[ProtoContract]
	public class SplitData
	{
		[ProtoMember(1)]
		public float Judge1Score = 0;
		[ProtoMember(2)]
		public float Judge2Score = 0;
		[ProtoMember(3)]
		public float Judge3Score = 0;

		public float TotalScore { get { return Judge1Score + Judge2Score + Judge3Score; } }

		public SplitData(float inJudgeScore1, float inJudgeScore2, float inJudgeScore3)
		{
			Judge1Score = inJudgeScore1;
			Judge2Score = inJudgeScore2;
			Judge3Score = inJudgeScore3;

		}

		public SplitData() { }

		public SplitData Duplicate()
		{
			return new SplitData(Judge1Score, Judge2Score, Judge3Score);
		}
	}

	[ProtoContract]
	public class LeaderNetData
	{
		[ProtoMember(1)]
		public string TeamName { get; set; }

		[ProtoMember(2)]
		public float TotalScore = 0;

		public LeaderNetData(string inName, float inTotalScore)
		{
			TeamName = inName;
			TotalScore = inTotalScore;
		}

		public LeaderNetData() { }
	}

	[ProtoContract]
	public class CounterData
	{
		[ProtoMember(1)]
		public int Count = 0;

		[ProtoMember(2)]
		public EFinish FinishState = EFinish.None;

		[ProtoMember(3)]
		public double RoutineTime = 0;

		public CounterData() { }

		public float GetFinalCount()
		{
			switch (FinishState)
			{
				case EFinish.Catch:
					return Count;
				case EFinish.Bobble:
					return Count / 2f;
				case EFinish.Drop:
					return 0;
			}

			return 0;
		}

		public string Serialise()
		{
			return Count + "," + FinishState;
		}

		public static CounterData DeSerialise(string InStr)
		{
			CounterData Ret = new CounterData();

			char[] Splitters = { ',' };
			string[] Mems = InStr.Split(Splitters, StringSplitOptions.RemoveEmptyEntries);
			if (Mems.Length == 2)
			{
				int.TryParse(Mems[0], out Ret.Count);
				Enum.TryParse<EFinish>(Mems[1], out Ret.FinishState);
			}

			return Ret;
		}
	}

	[ProtoContract]
	public class DiffNetData
	{
		[ProtoMember(1)]
		public float DiffScore = 0;

		[ProtoMember(2)]
		public double RoutineTime = 0;

		[ProtoMember(3)]
		public string JudgeId = "";

		public DiffNetData() { }

		public DiffNetData(string judgeId, float InDiffScore, double InTime)
		{
			JudgeId = judgeId;
			DiffScore = InDiffScore;
			RoutineTime = InTime;
		}
	}

    [ProtoContract]
    public class ClientConnectInfoNetData
    {
        [ProtoMember(1)]
        public string IpString = "";

        [ProtoMember(2)]
        public double RoutineLengthMinutes = 0;

        public ClientConnectInfoNetData() { }

        public ClientConnectInfoNetData(string ipString, double routineLengthMinutes)
        {
            IpString = ipString;
            RoutineLengthMinutes = routineLengthMinutes;
        }
    }

	public class ComboScore
	{
		public List<MoveScore> ComboPointsList = new List<MoveScore>();
		public EFinish FinishState = EFinish.None;
		public double FinishTime = -1;
		public float TotalPoints { get { return GetTotalPoints(); } }


		public float GetTotalPoints()
		{
			return GetTotalPoints(false);
		}

		public float GetTotalPoints(bool bIgnoreDeductions)
		{
			float Ret = 0;
			foreach (MoveScore ms in ComboPointsList)
			{
				Ret += ms.MovePoints;
			}

			if (ComboPointsList.Count > 0)
				Ret += ComboPointsList.Last().MovePoints;

			Ret *= ComboPointsList.Count * MainWindow.DiffPointsMulti;

			if (!bIgnoreDeductions)
			{
				switch (FinishState)
				{
					case EFinish.None:
						return Ret;
					case EFinish.Drop:
						return 0;
					case EFinish.Catch:
						return Ret;
					case EFinish.Bobble:
						return Ret / 2;
				}
			}

			return Ret;
		}
	}

	public class MoveScore
	{
		public float MovePoints { get { return (float)Math.Pow(MainWindow.DiffPowerBase, MoveDiff); } }
		public float MoveDiff = 0;
		public MoveNetData MoveData;

		public MoveScore() { }
		public MoveScore(float InDiff, MoveNetData InMoveData)
		{
			MoveDiff = InDiff;
			MoveData = InMoveData;
		}
	}

	[ProtoContract]
	public class MoveNetData
	{
		[ProtoMember(1)]
		public int MoveScore = 0;

		[ProtoMember(2)]
		public double RoutineTime = 0;

		public MoveNetData() { }

		public MoveNetData(int InMoveScore, double InTime)
		{
			MoveScore = InMoveScore;
			RoutineTime = InTime;
		}
	}

	public class DiffData
	{
		public int TotalDiff = 0;
		public EFinish FinishState = EFinish.None;

		public float GetFinalCount()
		{
			return TotalDiff;
		}
	}

	[ProtoContract]
	[ProtoInclude(3, typeof(JudgeCountData))]
	[ProtoInclude(4, typeof(JudgeDiffData))]
	[ProtoInclude(5, typeof(JudgeAiData))]
	[ProtoInclude(6, typeof(JudgeMusicData))]
	public abstract class JudgeDataBase
	{
		string _RoutineName = "";

		[ProtoMember(1)]
		public string RoutineName { get { return _RoutineName; }
			set
			{
				_RoutineName = value;
				Regex NumberReg = new Regex(@"^\d+.\s");
				Match NumberMatch = NumberReg.Match(_RoutineName);
				if (NumberMatch.Success)
					_RoutineName = _RoutineName.Replace(NumberMatch.Value, "");
			}
		}

		public ConnectionInfo ClientConnectionInfo = null;

		[ProtoMember(2)]
		public bool bTransfered = false;

		public abstract void ConfirmBackup();

		public virtual void SendBackup()
		{
			MainWindow.SendBackupCooldownTimer = 10f;
		}
	}

	[ProtoContract]
	public class JudgeCountData : JudgeDataBase
	{
		[ProtoMember(1, OverwriteList = true)]
		public List<CounterData> CountList { get; set; }

		[ProtoMember(2, OverwriteList = true)]
		public List<MoveNetData> MoveList { get; set; }

		public JudgeCountData()
		{
			CountList = new List<CounterData>();
			MoveList = new List<MoveNetData>();
		}

		public JudgeCountData(string InRoutineName)
		{
			RoutineName = InRoutineName;
			CountList = new List<CounterData>();
			MoveList = new List<MoveNetData>();
		}

		public override void SendBackup()
		{
			base.SendBackup();

			if (!bTransfered && MainWindow.bIsClientConnected)
				MainWindow.SendToServerNetData("BackupCount", this);
		}

		public override void ConfirmBackup()
		{
			if (ClientConnectionInfo != null && ClientConnectionInfo.ConnectionState == ConnectionState.Established)
			{
				MainWindow.SendToConnectionNetData("ConfirmBackupCount", ClientConnectionInfo, RoutineName);
			}
		}
	}

	[ProtoContract]
	public class JudgeDiffData : JudgeDataBase
	{
		[ProtoMember(1, OverwriteList = true)]
		public List<DiffNetData> DiffList { get; set; }

		public JudgeDiffData()
		{
			DiffList = new List<DiffNetData>();
		}

		public JudgeDiffData(string InRoutineName)
		{
			RoutineName = InRoutineName;
			DiffList = new List<DiffNetData>();
		}

		public override void SendBackup()
		{
			base.SendBackup();

			if (!bTransfered && MainWindow.bIsClientConnected)
				MainWindow.SendToServerNetData("BackupDiff", this);
		}

		public override void ConfirmBackup()
		{
			if (ClientConnectionInfo != null && ClientConnectionInfo.ConnectionState == ConnectionState.Established)
			{
				MainWindow.SendToConnectionNetData("ConfirmBackupDiff", ClientConnectionInfo, RoutineName);
			}
		}
	}

	[ProtoContract]
	public class JudgeAiData : JudgeDataBase
	{
		[ProtoMember(1)]
		public bool[] AiScores = Enumerable.Repeat(false, 8).ToArray();

		public JudgeAiData() { }

		public JudgeAiData(string InRoutineName)
		{
			RoutineName = InRoutineName;
		}

		public override void SendBackup()
		{
			base.SendBackup();

			if (!bTransfered && MainWindow.bIsClientConnected)
				MainWindow.SendToServerNetData("BackupAi", this);
		}

		public override void ConfirmBackup()
		{
			if (ClientConnectionInfo != null && ClientConnectionInfo.ConnectionState == ConnectionState.Established)
			{
				MainWindow.SendToConnectionNetData("ConfirmBackupAi", ClientConnectionInfo, RoutineName);
			}
		}
	}

	[ProtoContract]
	public class JudgeMusicData : JudgeDataBase
	{
		[ProtoMember(1)]
		public float MusicScore = 0;

		public JudgeMusicData() { }

		public JudgeMusicData(string InRoutineName)
		{
			RoutineName = InRoutineName;
		}

		public override void SendBackup()
		{
			base.SendBackup();

			if (!bTransfered && MainWindow.bIsClientConnected)
				MainWindow.SendToServerNetData("BackupMusic", this);
		}

		public override void ConfirmBackup()
		{
			if (ClientConnectionInfo != null && ClientConnectionInfo.ConnectionState == ConnectionState.Established)
			{
				MainWindow.SendToConnectionNetData("ConfirmBackupMusic", ClientConnectionInfo, RoutineName);
			}
		}
	}

	[ProtoContract]
	public class JudgeBackupData
	{
		[ProtoMember(1)]
		public IDictionary<string, JudgeCountData> BackupCountData = new Dictionary<string, JudgeCountData>();
		[ProtoMember(2)]
		public IDictionary<string, JudgeDiffData> BackupDiffData = new Dictionary<string, JudgeDiffData>();
		[ProtoMember(3)]
		public IDictionary<string, JudgeAiData> BackupAiData = new Dictionary<string, JudgeAiData>();
		[ProtoMember(4)]
		public IDictionary<string, JudgeMusicData> BackupMusicData = new Dictionary<string, JudgeMusicData>();

		public List<RoutineScore> CompleteRoutineScores = new List<RoutineScore>();

		public void CalcCompletedRoutines()
		{
			CompleteRoutineScores.Clear();

			foreach (KeyValuePair<string, JudgeCountData> CountDataIter in BackupCountData)
			{
				if (BackupDiffData.ContainsKey(CountDataIter.Value.RoutineName) && BackupAiData.ContainsKey(CountDataIter.Value.RoutineName) &&
					BackupMusicData.ContainsKey(CountDataIter.Value.RoutineName))
				{
					List<DiffNetData> RoutineDiffData = BackupDiffData[CountDataIter.Value.RoutineName].DiffList;
					List<ComboScore> NewRoutineComboScores = new List<ComboScore>();
					List<CounterData> RoutineCountData = CountDataIter.Value.CountList;
					List<MoveNetData> RoutineMoveData = CountDataIter.Value.MoveList;
					int DiffIndex = 0;
					for (int CountIndex = 0; CountIndex < RoutineCountData.Count; ++CountIndex)
					{
						CounterData cd = RoutineCountData[CountIndex];
						ComboScore NewComboScore = new ComboScore();

						NewComboScore.FinishState = cd.FinishState;
						NewComboScore.FinishTime = cd.RoutineTime;

						for (; DiffIndex < RoutineMoveData.Count; ++DiffIndex)
						{
							MoveNetData md = RoutineMoveData[DiffIndex];
							if (md.RoutineTime <= cd.RoutineTime + .01)
							{
								if (md.MoveScore > 0)
									NewComboScore.ComboPointsList.Add(new MoveScore(MainWindow.GetMoveDiff(RoutineDiffData, md.RoutineTime), md));
							}
							else
								break;
						}

						NewRoutineComboScores.Add(NewComboScore);
					}

					RoutineScore NewRoutineScore = new RoutineScore(CountDataIter.Value.RoutineName);
					//NewRoutineScore.Judge1Score = 
					//NewRoutineScore.RoutineComboScores = NewRoutineComboScores;
					//NewRoutineScore.AiScores = BackupAiData[CountDataIter.Value.RoutineName].AiScores;
					//NewRoutineScore.MusicScore = BackupMusicData[CountDataIter.Value.RoutineName].MusicScore * MainWindow.MusicPointsMulti;
					//NewRoutineScore.bPlayed = true;
					//NewRoutineScore.CalcScores();

					CompleteRoutineScores.Add(NewRoutineScore);
				}
			}
		}

		public void SendBackup<T>(IDictionary<string, T> InBackup) where T : JudgeDataBase
		{
			foreach (KeyValuePair<string, T> BackupData in InBackup)
			{
				if (!BackupData.Value.bTransfered)
				{
					BackupData.Value.SendBackup();
				}
			}
		}

		public void SendAllWaitingBackups()
		{
			SendBackup(BackupCountData);
			SendBackup(BackupDiffData);
			SendBackup(BackupAiData);
			SendBackup(BackupMusicData);
		}
	}

	[ProtoContract]
	public class C2SPingData
	{
	}

#endregion

	[ProtoContract]
	public class HatTeamNetData
	{
		[ProtoMember(1, OverwriteList = true)]
		public List<string> HatNames { get; set; }

		public HatTeamNetData()
		{
			HatNames = new List<string>();
		}
	}

	public enum EJudgerState
	{
		Ready,
		Judging,
		Finishing
	}
}
