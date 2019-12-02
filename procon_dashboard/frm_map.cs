using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace procon_dashboard
{
    public partial class frm_map : Form
    { 
        private string token { get; set; }
        private string host { get; set; }
        private Matche inf { get; set; }
        private MatchIDData data { get; set; }
        private DateTime timeServer { get; set; }
        private List<List<Button>> arrBtn;
        private Dictionary<Button, Agent> dictBtn;
        private bool startGame;
        private bool isSendRequset;
        private int currentTime;
        private long oldTime;
        private Button currentBtn;
        private ActionSubmit res;
        public frm_map()
        {
            InitializeComponent();
        }

        public frm_map(string token, string host, Matche inf)
        {
            this.token = token;
            this.host = host;
            this.inf = inf;
            InitializeComponent();
        }

        public DateTime UnixTimeToDateTime(long unixtime)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddMilliseconds(unixtime).ToLocalTime();
            return dtDateTime;
        }

        private void frm_map_Load(object sender, EventArgs e)
        {
            Console.WriteLine("host: " + host + " token:" + token);
            lb_titleID.Text = "Mã Trận Đấu: " + this.inf.id;
            lb_idTeam.Text = inf.teamID.ToString();
            lb_matchTo.Text = inf.matchTo;
            lb_turnMillis.Text = inf.turnMillis.ToString();
            lb_turns.Text = inf.turns.ToString();
            lb_intervalMillis.Text = inf.intervalMillis.ToString();
            lblCountDown.Text = "" + inf.turnMillis;
            this.startGame = true;

            // timer.Start();
            var task = MatchesID(host, token, inf.id);
            // Start auto refresh
            autoRefreshTimer.Interval = 1000;
            autoRefreshTimer.Start();
        }

        private void DrawMap(int hang, int cot, List<List<int>> tiled, List<List<int>> points, int myTeam)
        {
            this.currentBtn = null;
            int n = hang, m = cot;
            int height = panel_map.Height / n;
            int width = panel_map.Width / m;
            panel_map.AutoSize = true;
            panel_map.Controls.Clear();
            int vertical = 0, horizontal = 0;
            int dem = 0;
            this.arrBtn = new List<List<Button>>();
            this.dictBtn = new Dictionary<Button, Agent>();
            for (int i = 0; i < n; i++)
            {
                //khai bao list cot                
                List<Button> horizontalBtn = new List<Button>();
                //khai bao dict
                horizontal = 0;
                for (int j = 0; j < m; j++)
                {
                    dem++;
                    Button btn = new Button();
                    btn.Text = "" + points[i][j];
                    Color color = (tiled[i][j] == 0) ? Color.White : (tiled[i][j] == myTeam) ? Color.DeepSkyBlue : Color.Pink;
                    btn.BackColor = color;
                    btn.Click += new EventHandler(btn_click_agent);
                    btn.Size = new Size(width, height);
                    btn.Location = new Point(horizontal, vertical);
                    this.panel_map.Controls.Add(btn);

                    Agent agentTemp = new Agent(j, i, -1);
                    horizontalBtn.Add(btn);
                    dictBtn.Add(btn, agentTemp);

                    horizontal += width;
                }
                vertical += height;
                this.arrBtn.Add(horizontalBtn);
            }
        }

        private String getAction(Color color)
        {
            return (color == Color.Pink) ? "remove" : (color == Color.Red) ? "remove" : "move";
        }

        //su ly su kien bam quan co
        private void btn_click_agent(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            Console.WriteLine("btn_click_agent: " + btn.BackColor);
            Console.WriteLine("location: " + dictBtn[btn].agentID);
            Console.WriteLine("X: {0} Y: {1}", dictBtn[btn].x, dictBtn[btn].y);
            if (currentBtn != null)
            {
                int dx = dictBtn[btn].x - dictBtn[currentBtn].x;
                int dy = dictBtn[btn].y - dictBtn[currentBtn].y;
                Console.WriteLine("dx: {0} dy: {1}", dx, dy);
                //kiem tra co phai 8 o xung quanh
                if (-1 <= dx && dx <= 1 && -1 <= dy && dy <= 1)
                {
                    //update lai hanh hong cua agent
                    foreach (var item in this.res.actions)
                    {
                        if (item.agentID == dictBtn[currentBtn].agentID)
                        {
                            Console.WriteLine("Con thay doi: " + item.agentID);
                            item.dx = dx;
                            item.dy = dy;
                            item.type = getAction(btn.BackColor);
                        }
                    }
                    var json = JsonConvert.SerializeObject(this.res);
                    txt_jsonSubmit.Text = json;
                }
                currentBtn.BackColor = Color.Blue;
                currentBtn = null;
            }
            else
            if (btn.BackColor == Color.Blue)
            {
                this.currentBtn = btn;
                btn.BackColor = Color.Yellow;
            }


        }

        #region API
        public static string prevMatchesJson = "";
        public async Task MatchesID(string url, string token, int id)
        {
            Console.WriteLine("Lay thong tin tran dau: " + id);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Add("Authorization", this.token);
                //Console.WriteLine("---- Matches -----");
                //Console.WriteLine("toekn: " + token);
                var result = await client.GetAsync("matches/" + id);
                //Console.WriteLine("ket qua server tra ve: " + result);
                if (result.IsSuccessStatusCode)
                {
                    string body = await result.Content.ReadAsStringAsync();
                    if (body == prevMatchesJson)
                    {
                        return;
                    } else
                    {
                        prevMatchesJson = body;
                        lblCountDown.Text = "" + inf.turnMillis;
                    }
                    //Console.WriteLine("body: " + body);

                    JObject job = JObject.Parse(body);
                    data = job.ToObject<MatchIDData>();
                    //Console.WriteLine("heigth: " + data.height);
                    //Console.WriteLine("width: " + data.width;
                    int myTeamId = int.Parse(lb_idTeam.Text);
                    lb_currentTurn.Text = data.turn + "";
                    Console.WriteLine("Time server: " + data.startedAtUnixTime);

                    //bat dau dem nguoc de gui request
                    if (data.startedAtUnixTime != oldTime)
                    {
                        oldTime = data.startedAtUnixTime;
                        this.currentTime = (inf.turnMillis) / 1000;
                        //timerRequest.Start();
                    }
                    //isSendRequset = false;
                    //timeServer = UnixTimeToDateTime(data.startedAtUnixTime);
                    //Console.WriteLine("gio server truoc khi cong: "+timeServer);
                    //timeServer = timeServer.AddMilliseconds(inf.turnMillis + inf.intervalMillis);
                    //ket qua moi
                    res = new ActionSubmit();

                    foreach (var item in data.teams)
                    {
                        Console.WriteLine("team id: " + item.teamID);
                        if (item.teamID == myTeamId)
                        {
                            lb_teamIDTa.Text = item.teamID.ToString();
                            lb_areaPointTa.Text = item.areaPoint.ToString();
                            lb_tilePointTa.Text = item.tilePoint.ToString();
                        }
                        else
                        {
                            lb_teamIDDich.Text = item.teamID.ToString();
                            lb_areaPointDich.Text = item.areaPoint.ToString();
                            lb_tilePointDich.Text = item.tilePoint.ToString();
                        }
                    }
                    this.panel_map.Enabled = false;

                    //ve map 1 lan, doi request
                    if (!startGame) return;
                    Console.WriteLine("Ve Map");
                    DrawMap(data.height, data.width, data.tiled, data.points, myTeamId);
                    //ve agent hien tai
                    foreach (var item in data.teams)
                    {
                        //if (item.teamID == myTeamId)
                        //{
                        foreach (var agent in item.agents)
                        {
                            int x = agent.x - 1;
                            int y = agent.y - 1;
                            //Console.WriteLine("x: " + x + " y: " + y);
                            Button btn = this.arrBtn[y][x];
                            btn.BackColor = (item.teamID == myTeamId) ? Color.Blue : Color.Red;

                            // Console.WriteLine("button: " + btn.GetHashCode
                            if (dictBtn.ContainsKey(btn))
                            {
                                dictBtn[btn].agentID = agent.agentID;
                            }

                            //neu la quan minh thi them vao list ket qua
                            if (item.teamID == myTeamId)
                            {
                                this.res.actions.Add(new ActionSubmitFormat
                                {
                                    agentID = agent.agentID,
                                    dx = 0,
                                    dy = 0,
                                    turn = data.turn,
                                    type = "stay"
                                });
                            }
                        }

                        // }

                    }
                    //hien thi ket qua json
                    var json = JsonConvert.SerializeObject(this.res);
                    txt_jsonSubmit.Text = json;
                    this.panel_map.Enabled = true;
                    //auto refresh

                }

            }
        }

        public async Task Submit(string url, string token, int idMatch, string value)
        {
            Console.WriteLine("--- Submit ---");
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Add("Authorization", this.token);
                var data = new StringContent(value, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url + "matches/" + idMatch + "/action", data);
                string result = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine(result);
            }
        }
        #endregion
        private void timer_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("---");
            if (data.startedAtUnixTime == 0)
            {
                startGame = false;
                if (!isSendRequset)
                {
                    isSendRequset = true;
                    var task = MatchesID(host, token, inf.id);
                }


            }
            else
            if (data.startedAtUnixTime != 0)
            {
                if (!startGame)
                {
                    startGame = true;
                    oldTime = data.startedAtUnixTime;
                    timerRequest.Start();
                    MessageBox.Show("Bắt Đầu Trận Đấu !!!");
                    timer.Stop();

                }
            }
        }

        private void timerRequest_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("dem nguoc: " + currentTime);
            currentTime--;
            if (currentTime <= 0)
            {
                Console.WriteLine("New Requset to server");
                if (!isSendRequset)
                {
                    var task = MatchesID(host, token, inf.id);
                }
               
            }
        }

        private void btn_submit_Click(object sender, EventArgs e)
        {
            var task = Submit(host, token, inf.id, txt_jsonSubmit.Text);
        }


        
        private void button1_Click(object sender, EventArgs e)
        {
            var task = MatchesID(host, token, inf.id);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var task = Submit(host, token, inf.id, txt_jsonSubmit.Text);
        }
        private void autoRefreshTimer_Tick(object sender, EventArgs e)
        {
            lblCountDown.Text = (int.Parse(lblCountDown.Text) - 1000) + "";
            if (lblCountDown.Text == "1000")
            {
                _ = Submit(host, token, inf.id, txt_jsonSubmit.Text);
            }
            var task = MatchesID(host, token, inf.id);
        }
    }
}
