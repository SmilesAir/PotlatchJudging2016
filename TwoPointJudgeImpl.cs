using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

#pragma warning disable 0659

namespace Potlatch_Judger
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public string TwoPointJudgeId = "InvalidId";

		public float currentTwoPointScore = 0f;
		public float CurrentTwoPointScore
		{
			get { return currentTwoPointScore; }
			set
			{
				currentTwoPointScore = value;
				NotifyPropertyChanged("CurrentTwoPointScore");
				NotifyPropertyChanged("CurrentTwoPointScoreDisplay");
			}
		}
		public string CurrentTwoPointScoreDisplay
		{
			get { return CurrentTwoPointScore.ToString("0.0"); }
		}
		public float TwoPointIncreaseSpeed = .25f;
		public float TwoPointDecreaseSpeed = -8f;
		public float TwoPointNeutralSpeed = -1.5f;
		public float TwoPointIncreaseTriggerBonus = 1f;
		public float TwoPointDecreaseTriggerBonus = -2f;
		public float MaxTwoPointIncreaseTriggerBonus = .3f;
		public float MaxTwoPointDecreaseTriggerBonus = -1f;
		public float TwoPointIncreaseTriggerBonusFillSpeed = 100f;
		public float TwoPointDecreaseTriggerBonusFillSpeed = .2f;
		public bool bLeftTriggered = false;
		public bool bRightTriggered = false;
		public float SendDiffScoreInterval = .5f;
		public float CurrentDiffScoreInterval = 0f;

		TwoPointBackupData TwoPointBackup = null;
		List<DiffNetData> CurrentTwoPointBackupList = null;

		private void ResetTwoPoint()
		{
			CurrentDiffScoreInterval = 0f;
			CurrentTwoPointScore = 0f;
			TwoPointIncreaseTriggerBonus = MaxTwoPointIncreaseTriggerBonus;
			TwoPointDecreaseTriggerBonus = MaxTwoPointDecreaseTriggerBonus;
		}

		[DllImport("xinput1_4.dll")]
		public static extern int XInputGetState
			(
				int dwUserIndex,  // [in] Index of the gamer associated with the device
				ref XInputState pState        // [out] Receives the current state
			);

		void TwoPointStartRoutine()
		{
			if (TwoPointBackup == null)
			{
				TwoPointBackup = new TwoPointBackupData(TwoPointJudgeId);
			}

			string key = JudgeRoutineText;
			if (TwoPointBackup.Data.ContainsKey(key))
			{
				TwoPointBackup.Data.Remove(key);
			}

			TwoPointBackup.Data.Add(key, new TwoPointList());
			CurrentTwoPointBackupList = TwoPointBackup.Data[key].Data;
		}

		void TwoPointFinishRoutine()
		{
            try
            {
                using (var NewFile = File.Create(SaveFolderPath + "\\TwoPointBackup.bin"))
                {
                    TwoPointBackup.PackData();

                    Serializer.Serialize(NewFile, TwoPointBackup);
                }
            }
            catch
            {
            }
		}

		void SendTwoPointBackupData()
		{
            try
            {
			    string Filename = SaveFolderPath + "\\TwoPointBackup.bin";
			    if (File.Exists(Filename))
			    {
				    using (var NewFile = File.OpenRead(Filename))
				    {
					    TwoPointBackupData DiskBackup = Serializer.Deserialize<TwoPointBackupData>(NewFile);

					    SendToServerNetData("BackupTwoPoint", DiskBackup);
				    }
			    }
            }
            catch
            {
            }
		}

		private void MenuItem_SendTwoPointBackup(object sender, RoutedEventArgs e)
		{
			SendTwoPointBackupData();
		}

		private void DiffUpdateTick(float dt)
		{
			XInputState controllerState = new XInputState();
			XInputGetState(0, ref controllerState);

			TwoPointIncreaseTriggerBonus = Math.Min(MaxTwoPointIncreaseTriggerBonus, TwoPointIncreaseTriggerBonus + dt * TwoPointIncreaseTriggerBonusFillSpeed);
			TwoPointDecreaseTriggerBonus = Math.Max(MaxTwoPointDecreaseTriggerBonus, TwoPointDecreaseTriggerBonus - dt * TwoPointDecreaseTriggerBonusFillSpeed);

			float speed = TwoPointNeutralSpeed;

			if (controllerState.Gamepad.bRightTrigger > 100)
			{
				if (!bRightTriggered)
				{
					CurrentTwoPointScore += TwoPointIncreaseTriggerBonus;
					TwoPointIncreaseTriggerBonus = 0f;
				}

				bRightTriggered = true;
				speed = TwoPointIncreaseSpeed;
				DisplayDiffScore = "Good";
			}
			else
			{
				bRightTriggered = false;
			}

			if (controllerState.Gamepad.bLeftTrigger > 100)
			{
				if (!bLeftTriggered)
				{
					CurrentTwoPointScore += TwoPointDecreaseTriggerBonus;
					TwoPointDecreaseTriggerBonus = 0f;
				}

				bLeftTriggered = true;
				speed = TwoPointDecreaseSpeed;
				DisplayDiffScore = "Bad";
			}
			else
			{
				bLeftTriggered = false;
			}

			if ((!bLeftTriggered && !bRightTriggered))
			{
				DisplayDiffScore = "Neutral";
			}
			else if (bLeftTriggered && bRightTriggered)
			{
				DisplayDiffScore = "Both";
			}

			CurrentTwoPointScore = Math.Max(0f, CurrentTwoPointScore + speed * dt);

			DiffNetData dnd = new DiffNetData(TwoPointJudgeId, CurrentTwoPointScore, SecondsSinceRoutineStart);

			if (bIsClientConnected)
			{
				if (bRoutineRecording)
				{
					CurrentDiffScoreInterval += dt;
					if (CurrentDiffScoreInterval > SendDiffScoreInterval)
					{
						CurrentDiffScoreInterval = 0f;

						Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Threading.ThreadStart(() =>
						{
							SendToServerNetData("DiffResult", dnd);
						}));

						CurrentTwoPointBackupList.Add(dnd);

						TwoPointFinishRoutine();
					}
				}
			}
		}

		private float CalcTotalTwoPointScore()
		{
			float totalPoints = 0f;
			foreach (string judgeId in RecievedDiffData.Keys)
			{
				totalPoints += CalcJudgeTwoPointScore(judgeId);
			}

			return totalPoints;
		}

		private float CalcJudgeTwoPointScore(Dictionary<string, List<DiffNetData>> judgeData, string judgeId)
		{
			if (judgeData.ContainsKey(judgeId))
			{
				return CalcTwoPointScore(RecievedDiffData[judgeId]);
			}

			return 0f;
		}

		private float CalcJudgeTwoPointScore(string judgeId)
		{
			return CalcJudgeTwoPointScore(RecievedDiffData, judgeId);
		}

		float CalcAllJudgesTwoPointScore()
		{
			return CalcAllJudgesTwoPointScore(RecievedDiffData, -1f);
		}

		float CalcAllJudgesTwoPointScore(Dictionary<string, List<DiffNetData>> judgeData)
		{
			return CalcAllJudgesTwoPointScore(judgeData, -1f);
		}

		float CalcAllJudgesTwoPointScore(Dictionary<string, List<DiffNetData>> judgeData, float maxRoutineTime)
		{
			float CombinedTwoPointScore = 0f;
			foreach (var diffData in judgeData)
			{
				CombinedTwoPointScore += CalcTwoPointScore(diffData.Value, maxRoutineTime);
			}

			return CombinedTwoPointScore;
		}

		float CalcAllJudgesTwoPointScore(float maxRoutineTime)
		{
			return CalcAllJudgesTwoPointScore(RecievedDiffData, maxRoutineTime);
		}

		float CalcTwoPointScore(List<DiffNetData> multiplierList)
		{
			return CalcTwoPointScore(multiplierList, -1f);
		}

		float CalcTwoPointScore(List<DiffNetData> multiplierList, float maxRoutineTime)
		{
			float totalPoints = 0f;
			DiffNetData prevDnd = null;
			foreach (DiffNetData dnd in multiplierList)
			{
				if (maxRoutineTime < 0 || dnd.RoutineTime < maxRoutineTime)
				{
					if (prevDnd == null)
					{
						prevDnd = dnd;
					}
					else
					{
						float avgScore = dnd.DiffScore + prevDnd.DiffScore / 2f;
						double time = dnd.RoutineTime - prevDnd.RoutineTime;

						totalPoints += (float)(avgScore * time);

						prevDnd = dnd;
					}
				}
				else
				{
					break;
				}
			}

			return totalPoints;
		}

		float GetTwoPointDiffMultiplier(List<DiffNetData> diffData, double time)
		{
			for (int i = 0; i < diffData.Count; ++i)
			{
				if (diffData[i].RoutineTime > time)
				{
					if (i == 0)
					{
						return diffData[i].DiffScore;
					}
					else
					{
						double dataTimeDelta = diffData[i].RoutineTime - diffData[i - 1].RoutineTime;
						double timeDelta = time - diffData[i - 1].RoutineTime;
						float diffDelta = diffData[i].DiffScore - diffData[i - 1].DiffScore;
						return (float)(timeDelta / dataTimeDelta * diffDelta + diffData[i - 1].DiffScore);
					}
				}
			}

			return diffData.Count > 0 ? diffData[0].DiffScore : 0f;
		}

		float GetLastTwoPointMutliplier(string judgeId)
		{
			if (RecievedDiffData.ContainsKey(judgeId))
			{
				return RecievedDiffData[judgeId].Last().DiffScore;
			}

			return 0f;
		}
	}

	[ProtoContract]
	public class TwoPointBackupData
	{
		[ProtoMember(1)]
		public string JudgeId = "";
		[ProtoMember(2, OverwriteList = true)]
		public List<string> PackedKeyData { get; set; }
		[ProtoMember(3, OverwriteList = true)]
		public List<TwoPointList> PackedValueData { get; set; }


		public Dictionary<string, TwoPointList> Data = new Dictionary<string, TwoPointList>();

		public TwoPointBackupData()
		{
			PackedKeyData = new List<string>();
			PackedValueData = new List<TwoPointList>();
		}

		public TwoPointBackupData(string InJudgeId)
		{
			JudgeId = InJudgeId;

			PackedKeyData = new List<string>();
			PackedValueData = new List<TwoPointList>();
		}

		public void PackData()
		{
			PackedKeyData.Clear();
			PackedValueData.Clear();

			foreach (string key in Data.Keys)
			{
				PackedKeyData.Add(key);
			}

			foreach (var value in Data.Values)
			{
				PackedValueData.Add(value);
			}
		}

		public void UnPackData()
		{
			Data.Clear();

			for (int i = 0; i < PackedKeyData.Count; ++i)
			{
				Data.Add(PackedKeyData[i], PackedValueData[i]);
			}
		}
	}

	[ProtoContract]
	public class TwoPointList
	{
		[ProtoMember(1, OverwriteList = true)]
		public List<DiffNetData> Data = new List<DiffNetData>();
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct XInputGamepad
	{
		[MarshalAs(UnmanagedType.I2)]
		[FieldOffset(0)]
		public short wButtons;

		[MarshalAs(UnmanagedType.I1)]
		[FieldOffset(2)]
		public byte bLeftTrigger;

		[MarshalAs(UnmanagedType.I1)]
		[FieldOffset(3)]
		public byte bRightTrigger;

		[MarshalAs(UnmanagedType.I2)]
		[FieldOffset(4)]
		public short sThumbLX;

		[MarshalAs(UnmanagedType.I2)]
		[FieldOffset(6)]
		public short sThumbLY;

		[MarshalAs(UnmanagedType.I2)]
		[FieldOffset(8)]
		public short sThumbRX;

		[MarshalAs(UnmanagedType.I2)]
		[FieldOffset(10)]
		public short sThumbRY;


		public bool IsButtonPressed(int buttonFlags)
		{
			return (wButtons & buttonFlags) == buttonFlags;
		}

		public bool IsButtonPresent(int buttonFlags)
		{
			return (wButtons & buttonFlags) == buttonFlags;
		}



		public void Copy(XInputGamepad source)
		{
			sThumbLX = source.sThumbLX;
			sThumbLY = source.sThumbLY;
			sThumbRX = source.sThumbRX;
			sThumbRY = source.sThumbRY;
			bLeftTrigger = source.bLeftTrigger;
			bRightTrigger = source.bRightTrigger;
			wButtons = source.wButtons;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is XInputGamepad))
				return false;
			XInputGamepad source = (XInputGamepad)obj;
			return ((sThumbLX == source.sThumbLX)
			&& (sThumbLY == source.sThumbLY)
			&& (sThumbRX == source.sThumbRX)
			&& (sThumbRY == source.sThumbRY)
			&& (bLeftTrigger == source.bLeftTrigger)
			&& (bRightTrigger == source.bRightTrigger)
			&& (wButtons == source.wButtons));
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct XInputState
	{
		[FieldOffset(0)]
		public int PacketNumber;

		[FieldOffset(4)]
		public XInputGamepad Gamepad;

		public void Copy(XInputState source)
		{
			PacketNumber = source.PacketNumber;
			Gamepad.Copy(source.Gamepad);
		}

		public override bool Equals(object obj)
		{
			if ((obj == null) || (!(obj is XInputState)))
				return false;
			XInputState source = (XInputState)obj;

			return ((PacketNumber == source.PacketNumber)
				&& (Gamepad.Equals(source.Gamepad)));
		}
	}
}
