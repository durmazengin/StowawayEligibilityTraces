using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StowawayEligibilityTraces
{
    public partial class MainForm : Form
    {
        Environment environment = null;
        const int MAX_ITER_IN_EPISODE = 10000;// except first episode
        
        const double INITIAL_EPSILON_VALUE = 0.2;
        const double ALPHA = 0.12;
        const double GAMMA = 0.8;
        const double LAMBDA = 0.4;

        double epsilon = 0.25;

        const int CONVERGE_LIMIT = 10;
        /*
         * when best path N; and last 9 is N, 1 is N+2
         * mean = (N * 9 + N + 2) / 10 = N + 0.2
         * var = (0.2^2 * 9 + 1.8^2 = 0.36 + 3.24) / 10 = 0.36
         */
        const double CONVERGE_VARIANCE_PATH = 0.33;
        const double CONVERGE_VARIANCE_AVG_REWARD = 0.36;
        const int CONVERGE_PTS_FACTOR = 100000;

        List<State> statesVisited = new List<State>();
        List<int> bestPath = null;
        List<int> last10EpisodeStepCount = new List<int>();
        List<double> last10EpisodeAvgRewards = new List<double>();

        int episodeNr = 0;

        public MainForm()
        {
            InitializeComponent();

            environment = new Environment(this.pnlGameField);

            numerHorizontalGrids.Value = 30;
            numerVerticalGrids.Value = 30;

            rdBtnConvergePath.Checked = true;
        }

        private void btnSelectFillColor_Click(object sender, EventArgs e)
        {
            if (clrDialogSelector.ShowDialog() == DialogResult.OK)
            {
                btnSelectFillColor.BackColor = clrDialogSelector.Color;

                Prepare();
            }
        }

        private void btnSelectLineColor_Click(object sender, EventArgs e)
        {
            if (clrDialogSelector.ShowDialog() == DialogResult.OK)
            {
                btnSelectLineColor.ForeColor = clrDialogSelector.Color;

                Prepare();
            }
        }

        private void numerHorizontalGrids_ValueChanged(object sender, EventArgs e)
        {
            Prepare();
        }

        private void numerVerticalGrids_ValueChanged(object sender, EventArgs e)
        {
            Prepare();
        }

        private void numerStartPositionX_ValueChanged(object sender, EventArgs e)
        {
            environment.setAgentStartPosition((int)numerStartPositionX.Value, (int)numerStartPositionY.Value);
        }

        private void numerStartPositionY_ValueChanged(object sender, EventArgs e)
        {
            environment.setAgentStartPosition((int)numerStartPositionX.Value, (int)numerStartPositionY.Value);
        }

        private void numerGoalPositionX_ValueChanged(object sender, EventArgs e)
        {
            environment.setGoalPosition((int)numerGoalPositionX.Value, (int)numerGoalPositionY.Value);
        }

        private void numerGoalPositionY_ValueChanged(object sender, EventArgs e)
        {
            environment.setGoalPosition((int)numerGoalPositionX.Value, (int)numerGoalPositionY.Value);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            Prepare();
            environment.setGoalPosition((int)numerGoalPositionX.Value, (int)numerGoalPositionY.Value);

            environment.setAgentStartPosition((int)numerStartPositionX.Value, (int)numerStartPositionY.Value);

            cmbWindDirection.SelectedIndex = 0;
        }

        private void Prepare()
        {
            numerStartPositionX.Maximum = numerVerticalGrids.Value;
            numerStartPositionY.Maximum = numerHorizontalGrids.Value;

            numerGoalPositionX.Maximum = numerVerticalGrids.Value;
            numerGoalPositionY.Maximum = numerHorizontalGrids.Value;

            environment.drawGrids((int)numerHorizontalGrids.Value,
                (int)numerVerticalGrids.Value,
                btnSelectLineColor.ForeColor,
                btnSelectFillColor.BackColor);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            resetStates();
        }

        private void resetStates()
        {
            statesVisited = new List<State>();
            bestPath = null;

            txtLogs.Clear();
            episodeNr = 0;
            epsilon = INITIAL_EPSILON_VALUE;
        }

        private void btnReport_Click(object sender, EventArgs e)
        {
            ReportForm reportForm = new ReportForm(statesVisited, (int)numerHorizontalGrids.Value, (int)numerVerticalGrids.Value);
            reportForm.ShowDialog();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            environment.onAppStopped();
        }

        private void cmbWindDirection_SelectedIndexChanged(object sender, EventArgs e)
        {
            WindDirection direction = (WindDirection)cmbWindDirection.SelectedIndex;
            if (direction == WindDirection.East || direction == WindDirection.West)
            {
                lblWindLocation.Text = "Row Number";
            }
            else
            {
                lblWindLocation.Text = "Column Number";
            }
            environment.setWind((int)numerWindToBeSet.Value, direction, (int)numerWindLocation.Value);
        }

        private void numerWindLocation_ValueChanged(object sender, EventArgs e)
        {
            // wind location 0 means no such wind
            environment.setWind((int)numerWindToBeSet.Value, (WindDirection)cmbWindDirection.SelectedIndex, (int)numerWindLocation.Value);
        }

        private void numerWindToBeSet_ValueChanged(object sender, EventArgs e)
        {
            Wind wind = environment.getCurrentWind((int)numerWindToBeSet.Value);
            if (wind == null)
            {
                numerWindLocation.Value = 0;// 0 means not set
            }
            else
            {
                numerWindLocation.Value = wind.getPosition();
                cmbWindDirection.SelectedIndex = (int)wind.getDirection();
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
#if HUNDRED_EPISODE_TEST
            String strFileName = String.Format("Episodes{0:YYMMDDHHmmss}.txt", DateTime.Now);
            List<int> avarageCounts = new List<int>();
           
            System.IO.File.Create(strFileName).Close();
            
            System.IO.File.AppendAllText(strFileName, String.Format("LAMBDA : {0}\n", LAMBDA));
            for (int j = 0; j < 100; j++)
            {
                resetStates();
                int iterCount = runForLamda();
                System.IO.File.AppendAllText(strFileName, String.Format("{0}\n", iterCount));
            }
            System.IO.File.AppendAllText(strFileName, "\n");
#else
            runForLamda();
#endif
        }

        private int runForLamda()
        {

            bool isConverged = false;
            int convergeCounter = 0;
            for (int i = 0; i < numerSuccessIterations.Value; i++)
            {
                Prepare();

                List<State> updatedStates = Train(statesVisited);
                if (updatedStates == null)
                {
                    txtLogs.AppendText("IN LOOP BREAK");
                    return runForLamda();
                }
                statesVisited = updatedStates;
                isConverged = checkConvergence();
                if (isConverged)
                {
                    convergeCounter = i;
                    break;// end training
                }
            }
            if (!isConverged)
            {
                txtLogs.AppendText("CURRENT BEST PATH IS : " + bestPath.Count);
            }
            return convergeCounter;
        }

        // State is an object holding its value and points to next states when action taken
        private List<State> Train(List<State> statesLearned)
        {
            episodeNr++;
            State currentState = null;

            // initalize elibility traces for this episode
            foreach (State visitedState in statesLearned)
            {
                visitedState.setET(0);
            }

            if (0 == statesLearned.Count)
            {
                currentState = new State((int)numerStartPositionX.Value, (int)numerStartPositionY.Value);
                statesLearned.Add(currentState);
            }
            else
            {
                currentState = statesLearned[0];
            }
            EnvironmentResponse envResponse = environment.tryAction(currentState.getXPosition(), currentState.getYPosition());

            int iterationCount = 0;
            
            // try first action
            int currentAction = 0;
            int maxRewardIndex = 0;

            double[] qCurrent = new double[Action.COUNT];// Q values of current action
            for (int iqc = 0; iqc < Action.COUNT; iqc++)
            {
                qCurrent[iqc] = currentState.getActionReward(iqc);
                if (qCurrent[maxRewardIndex] < qCurrent[iqc])
                {
                    maxRewardIndex = iqc;
                }
            }

            // select first action greedy q-values
            currentAction = SelectActionGreedy(qCurrent, maxRewardIndex);

            while (true)
            {
                State nextState = GetNextStateReference(currentState, currentAction);
                // take action
                envResponse = environment.tryAction(nextState.getXPosition(), nextState.getYPosition());

                iterationCount++;
                if (envResponse == EnvironmentResponse.NOT_AVAILABLE) // action could not be available
                {
                    currentAction = SelectActionGreedy(qCurrent, maxRewardIndex);
                    // do nothing
                }
                else
                {
                    /*
                     * before wind included, we could calculate the agent position
                     * after wind affect, we should ask position to environment
                     * because may be changed by wind
                     */
                    Point agentPosition = environment.getAgentPosition();
                    if ((agentPosition.X != nextState.getXPosition()) || (agentPosition.Y != nextState.getYPosition()))
                    {
                        String strLogWind = String.Format("Wind from ({0:D2}, {1:D2}) to ({2:D2}, {3:D2})",
                            nextState.getXPosition(), nextState.getYPosition(), agentPosition.X, agentPosition.Y);
                        txtLogs.AppendText(strLogWind + "\r\n");

                        nextState = new State(agentPosition.X, agentPosition.Y);
                    }

                    if (statesLearned.Contains(nextState))// check if state visited before
                    {
                        int index = statesLearned.IndexOf(nextState);
                        nextState = statesLearned[index];
                        currentState.setNextSate(currentAction, index);
                    }
                    else
                    {
                        int index = statesLearned.Count;
                        statesLearned.Add(nextState);
                        currentState.setNextSate(currentAction, index);
                    }
                    currentState.setLastAction(currentAction);
                    currentState.setET(currentState.getET() + 1);

                    double reward = 0;
                    double delta = 0;
                    
                    if (envResponse == EnvironmentResponse.GOAL)
                    {
                        reward = 1;
                        delta = reward - currentState.getActionReward(currentAction);
                        currentState.setActionReward(currentAction, currentState.getActionReward(currentAction) + delta * ALPHA * currentState.getET());
                        break;
                    }

                    double [] qNext = new double[Action.COUNT];// Q values of next action
                    for (int iqn = 0; iqn < Action.COUNT; iqn++)
                    {
                        qNext[iqn] = nextState.getActionReward(iqn);
                        if (qNext[maxRewardIndex] < qNext[iqn])
                        {
                            maxRewardIndex = iqn;
                        }
                    }
                    // select next action greedy q-values
                    int actionNext = SelectActionGreedy(qNext, maxRewardIndex);
                    
                    delta = reward + GAMMA * nextState.getActionReward(actionNext) - currentState.getActionReward(currentAction);
                    currentState.setActionReward(currentAction, currentState.getActionReward(currentAction) + delta * ALPHA * currentState.getET());
                    for(int iET = 0; iET < statesLearned.Count; iET++)
                    {
                        statesLearned[iET].setET(statesLearned[iET].getET() * GAMMA * LAMBDA);
                    }
                    currentState = nextState;
                    currentAction = actionNext;
                }


                // check if endless loop
                if (episodeNr > 1 && iterationCount > MAX_ITER_IN_EPISODE)
                {
                    return null;// ignore this episode
                }
            }
            if (epsilon > 0.01)
            {
                epsilon = epsilon * 0.999;
            }
            // update cumulative episode

            double averageReward = 0;
            State visitingState = statesLearned[0];
            int i = 0;
            List<int> episodePath = new List<int>();// for statistical, oath from start to end
            while (true)
            {
                int lastAction = visitingState.getLastAction();
                if (lastAction < 0)
                {
                    break;
                }
                averageReward += visitingState.getActionReward(lastAction);
                int stateRef = visitingState.getNextSate(visitingState.getLastAction());
                visitingState = statesLearned[stateRef];
                i++;
                episodePath.Add(lastAction);
            }
            averageReward = averageReward / i;

            if ((bestPath == null) || (bestPath.Count >= episodePath.Count))
            {
                bestPath = episodePath;
            }
            
            String strLog = String.Format("Iteration {0:D3}: {1:D5}", episodeNr, iterationCount);
            txtLogs.AppendText(strLog + "\r\n");
            txtLogs.Refresh();
            
            if (rdBtnConvergePath.Checked)
            {
                if (last10EpisodeStepCount.Count >= CONVERGE_LIMIT)
                {
                    last10EpisodeStepCount.RemoveAt(0);
                }
                last10EpisodeStepCount.Add(iterationCount);
            }
            else
            {
                if (last10EpisodeAvgRewards.Count >= CONVERGE_LIMIT)
                {
                    last10EpisodeAvgRewards.RemoveAt(0);
                }
                last10EpisodeAvgRewards.Add(averageReward * CONVERGE_PTS_FACTOR);
            }
            return statesLearned;
        }
        
        private State GetNextStateReference(State currentState, int selectedAction)
        {
            State nextState = null;

            switch (selectedAction)
            {
                case Action.LEFT:
                    nextState = new State(currentState.getXPosition() - 1, currentState.getYPosition());
                    break;
                case Action.UP:
                    nextState = new State(currentState.getXPosition(), currentState.getYPosition() - 1);
                    break;
                case Action.RIGHT:
                    nextState = new State(currentState.getXPosition() + 1, currentState.getYPosition());
                    break;
                case Action.DOWN:
                    nextState = new State(currentState.getXPosition(), currentState.getYPosition() + 1);
                    break;
            }

            return nextState;
        }

        private int SelectActionGreedy(double[] qValues, int maxRewardIndex)
        {
            int selectedAction = -1;
            // if this is not first episode
            if (qValues[maxRewardIndex] > 0)
            {
                /*
                 * a_star = Q(s, a)
                 * policy(s, a) = 
                 *          1 - e + e/NumofActions if a = a_star
                 *          e/NumofActions         if a != a_star
                 */
                double probMax = 1 - epsilon + epsilon / Action.COUNT;
                double probOther = epsilon / Action.COUNT;

                double random = (double)Utils.GetRandomNumber(0, 100) / 100;
                if (probMax > random) // random <  85
                {
                    selectedAction = maxRewardIndex;
                }
                else if (probMax + probOther > random) // random < 90
                {
                    selectedAction = (maxRewardIndex + 1) % Action.COUNT;
                }
                else if (probMax + 2 * probOther > random) // random < 95
                {
                    selectedAction = (maxRewardIndex + 2) % Action.COUNT;
                }
                else
                {
                    selectedAction = (maxRewardIndex + 3) % Action.COUNT;
                }
            }
            else
            {
                // if this is first episode
                selectedAction = Utils.GetRandomNumber(0, Action.COUNT);
            }
            if (selectedAction == 3)
            {
            }
            if (selectedAction == 4)
            {
                selectedAction = 0;
            }
            return selectedAction;
        }


        private bool checkConvergence()
        {
            bool isConverged = false;
            if (rdBtnConvergePath.Checked)
            {
                if (last10EpisodeStepCount.Count >= CONVERGE_LIMIT)
                {
                    double cov = Utils.calculateCov(last10EpisodeStepCount);
                    if (cov <= CONVERGE_VARIANCE_PATH)
                    {
                        isConverged = true;
                        txtLogs.AppendText("IT IS CONVERGED and BEST PATH IS : " + bestPath.Count);
                    }
                }
            }
            else
            {
                if (last10EpisodeAvgRewards.Count >= CONVERGE_LIMIT)
                {
                    double cov = Utils.calculateCov(last10EpisodeAvgRewards);
                    if (cov <= CONVERGE_VARIANCE_AVG_REWARD)
                    {
                        isConverged = true;
                        txtLogs.AppendText("CONVERGED AVG POINT is " + last10EpisodeAvgRewards[0] / CONVERGE_PTS_FACTOR);
                        txtLogs.AppendText("\r\nand BEST PATH is " + bestPath.Count);
                    }
                }

            }
            return isConverged;
        }
    }
}
