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
using System.Windows.Shapes;
using System.IO;
using System.ComponentModel;

namespace Potlatch_Judger
{
    /// <summary>
    /// Interaction logic for HatSorter.xaml
    /// </summary>
    public partial class HatSorter : Window, INotifyPropertyChanged
    {
        public bool bAllowClosing = false;
        public string AText = "";
        public string BText = "";
        public string ResultsText = "";
        bool bLoading = true;
        public event PropertyChangedEventHandler PropertyChanged;
        string groupAStatusText = "";
        public string GroupAStatusText { get { return groupAStatusText; } set { groupAStatusText = value; NotifyPropertyChanged("GroupAStatusText"); } }
        string groupBStatusText = "";
        public string GroupBStatusText { get { return groupBStatusText; } set { groupBStatusText = value; NotifyPropertyChanged("GroupBStatusText"); } }

        public HatSorter()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bLoading = true;

            AText = Properties.Settings.Default.GroupAText;
            BText = Properties.Settings.Default.GroupBText;
            ResultsText = Properties.Settings.Default.ResultsText;

            GroupAText.Text = AText;
            GroupBText.Text = BText;

            bLoading = false;

            FillResults();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();

            if (!bAllowClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void NotifyPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void FillResults()
        {
            if (bLoading)
                return;

            AText = GroupAText.Text;
            BText = GroupBText.Text;

            StringReader AReader = new StringReader(AText);
            StringReader BReader = new StringReader(BText);

            List<string> ANames = new List<string>();
            List<string> BNames = new List<string>();

            int NumANames = 0;
            string line = null;
            while ((line = AReader.ReadLine()) != null)
            {
                NumANames += line.Trim().Length > 0 ? 1 : 0;
                ANames.Add(line.Trim());
            }

            int NumBNames = 0;
            while ((line = BReader.ReadLine()) != null)
            {
                NumBNames += line.Trim().Length > 0 ? 1 : 0;
                BNames.Add(line.Trim());
            }

            int PlayersPerTeam = 2;
            int NumPlayers = NumANames + NumBNames;
            int NumTeams = NumPlayers / PlayersPerTeam;

            ResultsText = "";
            Random Rand = new Random();

            if ((NumPlayers % 2) == 0)
            {
                GroupAStatusText = "Good - " + ANames.Count + " Names";
                GroupBStatusText = "Good - " + BNames.Count + " Names";
            }
            else
            {
                GroupAStatusText = "Bad - " + ANames.Count + " Names";
                GroupBStatusText = "Bad - " + BNames.Count + " Names";
            }


            if (ANames.Count == NumTeams)
            {
                for (int TeamIndex = 0; TeamIndex < NumTeams; ++TeamIndex)
                {
                    int AIndex = Rand.Next() % ANames.Count;
                    ResultsText += ANames[AIndex] + " - ";
                    ANames.RemoveAt(AIndex);

                    int BIndex = Rand.Next() % BNames.Count;
                    ResultsText += BNames[BIndex] + "\r\n";
                    BNames.RemoveAt(BIndex);
                }
            }
            else
                ResultsText = "Teams Error";

            TeamsResultsText.Text = ResultsText;

            Properties.Settings.Default.GroupAText = AText;
            Properties.Settings.Default.GroupBText = BText;
            Properties.Settings.Default.ResultsText = ResultsText;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FillResults();
        }

        private void GroupAText_TextChanged(object sender, TextChangedEventArgs e)
        {
            FillResults();
        }

        private void GroupBText_TextChanged(object sender, TextChangedEventArgs e)
        {
            FillResults();
        }
    }
}
