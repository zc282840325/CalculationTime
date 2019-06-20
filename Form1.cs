using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace CalculationTime
{
    public partial class Form1 : Form
    {
        public static List<Entity> list = new List<Entity>();
        public static string msg = "";
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button3.Enabled = false;
            //去掉头部的空白头
            dataGridView1.RowHeadersVisible = false;
            dataGridView2.RowHeadersVisible = false;
            //加载log4net配置文件
            var filepath = AppDomain.CurrentDomain.BaseDirectory + "log4net.config";
            log4net.Config.XmlConfigurator.Configure(new FileInfo(filepath));
            LogHelper.Info("启动程序");
        }
        //计算时效值
        private void Button1_Click(object sender, EventArgs e)
        {
            LogHelper.Info("计算时效值");
            Init();

            Thread thread = new Thread(Start);
            thread.Start();
        }
        //统计总和
        private void Button2_Click(object sender, EventArgs e)
        {
            LogHelper.Info("统计总和");
            msg += "\r\n统计总和\r\n";
            msg += "时间：\t\t\t\t名称：\t\t\t\t总数量：\t\t\t\t总时效值(单位秒)：\r\n";
            dataGridView2.AutoGenerateColumns = false;
            //统计总和
            List<Entity> listname = list.Distinct(new Compare()).ToList();
            for (int i = 0; i < listname.Count; i++)
            {
                //查询某一个人的值班记录
                List<Entity> entities = list.Where(o => o.Yjfb == listname[i].Yjfb).ToList();
                for (int j = 0; j < entities.Count; j++)
                {
                    listname[i].Count += entities[j].Count;
                    listname[i].Totalvalue += entities[j].Totalvalue;
                }
                listname[i].Date = DateTime.Parse(dateTimePicker1.Text).AddMonths(-1);
                msg += listname[i].Date+ "\t\t" + listname[i].Yjfb+ "\t\t\t\t" + listname[i].Count+ "\t\t\t\t" + listname[i].Totalvalue+"\r\n";
            }
            //绑定数据
            dataGridView2.DataSource = listname;
            button2.Enabled = false;
            button3.Enabled = true;
        }
        //导出结果
        private void Button3_Click(object sender, EventArgs e)
        {
            LogHelper.Info("导出结果");
            SaveProject();
        }

        #region 初始化
        private void Init()
        {
            msg = "";
            dataGridView1.AutoGenerateColumns = false;
            button1.Enabled = false;
            button3.Enabled = false;
            button2.Enabled = false;
            dateTimePicker1.Enabled = false;
            list = null;
            dataGridView1.DataSource = null;
            dataGridView2.DataSource = null;
        }
        #endregion

        #region 启动程序
        public void Start()
        {
            //数据库链接地址
            string dlweather = "Data Source=129.211.11.64;Initial Catalog=dlweather;Persist Security Info=True;User ID=sa;Password=4rfv%TGB";
            string warninfoDlNew = "Data Source=129.211.11.64;Initial Catalog=warninfoDlNew;Persist Security Info=True;User ID=sa;Password=4rfv%TGB";
            //声明对象
            string starttime = string.Empty;
            string endtime = string.Empty;
            int count = 0;
            //声明数据库连接对象
            SqlHelper dlweather_sql = new SqlHelper(dlweather);
            SqlHelper warninfoDlNew_sql = new SqlHelper(warninfoDlNew);
            //获取时间控件的值
            string nowtime = dateTimePicker1.Text;
            //时间格式转换
            DateTime dtime = DateTime.Parse(nowtime);
            //计算上一个月的月初和月末
            GetSartAndEnd(ref starttime, ref endtime, dtime, ref count);
            //查询值班人员记录
            string sql = string.Format("select date,yjfb from duty where date BETWEEN '{0}' and '{1}'", starttime, endtime);
            //获取数据
            DataTable dt = new DataTable();
            try
            {
                dt = dlweather_sql.ExcuteDataTable(sql);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex.Message);
            }
            if (dt.Rows.Count>0)
            {
            //DataTable数据转换到List<Entity>
            list = ConvertToEx<Entity>(dt);
            msg += "时间：" + starttime + "--" + endtime + "的值班记录如下:\r\n";
            msg += "时间：\t\t\t\t名称：\t\t\t\t数量：\t\t\t\t平均时效值：\t\t\t\t总时效值(单位秒)：\r\n";
                //计算时效值
                for (int i = 0; i < list.Count; i++)
                {
                    Entity entity = list[i];
                    CalculationTimes(entity, warninfoDlNew_sql);
                }
                //绑定数据
                this.Invoke(new EventHandler(delegate
                {
                    dataGridView1.DataSource = list;
                    button2.Enabled = true;
                    button1.Enabled = true;
                    dateTimePicker1.Enabled = true;
                }));
            }
            else
            {
                this.Invoke(new EventHandler(delegate
                {
                    button1.Enabled = true;
                    dateTimePicker1.Enabled = true;
                }));
                MessageBox.Show("没数据，请重新选择时间！");
            }
        }
        #endregion

        #region 获取上个月的时间区间
        /// <summary>
        /// 
        /// </summary>
        /// <param name="starttime">输出月初时间</param>
        /// <param name="endtime">输出月末时间</param>
        /// <param name="dt">当前输入的时间点</param>
        public void GetSartAndEnd(ref string starttime,ref string endtime, DateTime dt,ref int count)
        {
            //获取下一个月又多少天
            count = DateTime.DaysInMonth(dt.Year, dt.AddMonths(-1).Month);
            //1月的上一个月跨年，逻辑调整
            string year = string.Empty;
            if (dt.Month == 1)
            {
                year = dt.AddYears(-1).Year.ToString();
            }
            else
            {
                year = dt.Year.ToString();
            }

            starttime = year+"-" + dt.AddMonths(-1).Month + "-01";
            endtime= year+"-" + dt.AddMonths(-1).Month + "-" + count;
        }
        #endregion

        #region DataTable转换为List<T>
        public static List<T> ConvertToEx<T>(DataTable dt) where T : new()
        {
            if (dt == null) return null;
            if (dt.Rows.Count <= 0) return null;

            List<T> list = new List<T>();
            Type type = typeof(T);
            PropertyInfo[] propertyInfos = type.GetProperties();  //获取泛型的属性
            List<DataColumn> listColumns = dt.Columns.Cast<DataColumn>().ToList();  //获取数据集的表头，以便于匹配
            T t;
            foreach (DataRow dr in dt.Rows)
            {
                t = new T();
                foreach (PropertyInfo propertyInfo in propertyInfos)
                {
                    try
                    {
                        DataColumn dColumn = listColumns.Find(name => name.ToString().ToUpper() == propertyInfo.Name.ToUpper());  //查看是否存在对应的列名
                        if (dColumn != null)
                            propertyInfo.SetValue(t, dr[propertyInfo.Name], null);  //赋值
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
                list.Add(t);
            }
            return list;
        }
        #endregion

        #region List去重复
        public class Compare : IEqualityComparer<Entity>
        {
            public bool Equals(Entity x, Entity y)
            {
                return x.Yjfb == y.Yjfb;//可以自定义去重规则，此处将Id相同的就作为重复记录，不管学生的爱好是什么
            }
            public int GetHashCode(Entity obj)
            {
                return obj.Yjfb.GetHashCode();
            }
        }
        #endregion

        #region 计算每天的时效值
        /// <summary>
        /// 计算每天的时效值
        /// </summary>
        /// <param name="entity">实体对象</param>
        /// <param name="warninfoDlNew_sql">数据库链接对象</param>
        public void CalculationTimes(Entity entity, SqlHelper warninfoDlNew_sql)
        {
            string startwork = entity.Date.ToString("yyyy-MM-dd") + " 08:00:00.000";
            string endwork = entity.Date.AddDays(1).ToString("yyyy-MM-dd") + " 08:00:00.000";
            string sql2 = string.Format("SELECT (Select CONVERT(varchar(100), updated, 20)) as updated,(Select CONVERT(varchar(100), created, 20)) as created from WI_warninfod where status = '发布完成' and other = 'T_ALARM' and updated BETWEEN '{0}' and '{1}' Select CONVERT(varchar(100), GETDATE(), 21)", startwork, endwork);
            DataTable dt2 = new DataTable();
            try
            {
                dt2 = warninfoDlNew_sql.ExcuteDataTable(sql2);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex.Message);
            }
            entity.Count = dt2.Rows.Count;
            double df = 0.0;
            for (int j = 0; j < dt2.Rows.Count; j++)
            {
                DateTime t1 = DateTime.Parse(dt2.Rows[j]["updated"].ToString());
                DateTime t2 = DateTime.Parse(dt2.Rows[j]["created"].ToString());
                TimeSpan ts = t1 - t2;
                df += ts.TotalSeconds;
            }
            if (dt2.Rows.Count > 0)
            {
                int ms = Convert.ToInt32(df / dt2.Rows.Count);
                entity.shixiao = formatTime(ms);
                entity.Totalvalue = df;
            }
            else
            {
                entity.shixiao = "0";
                entity.Totalvalue = 0.0;
            }
            msg += entity.Date + "\t\t" + entity.Yjfb + "\t\t\t\t" + entity.Count + "\t\t\t\t" + entity.shixiao + "\t\t\t\t\t" + entity.Totalvalue + "\r\n";
        }
        #endregion

        #region 秒转换为时、分、秒
        public static string formatTime(int ms)
        {
            int ss = 1;
            int mi = ss * 60;
            int hh = mi * 60;

            int hour = Convert.ToInt32(ms / hh);
            int minute = Convert.ToInt32((ms - hour * hh) / mi);
            int second = Convert.ToInt32((ms - hour * hh - minute * mi) / ss);
            return hour + ":" + minute + ":" + second;
        }
        #endregion

        #region 实体模型
        public class Entity
        {
            public string Yjfb { get; set; }//员工姓名
            public DateTime Date { get; set; }//值班时间
            public int Count { get; set; }//1天复合条数
            public string shixiao { get; set; }//时效值(AVG)
            public double Totalvalue { get; set; }//当前日期时效总值
        }



        #endregion

        #region 打开保存对话框
        /// <summary>
        /// 保存文件对话框
        /// </summary>
        public static void SaveProject()
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.AddExtension = true;

            saveFileDialog1.Filter = "文本文件|*.txt"; //文件类型

            saveFileDialog1.Title = "记录";//标题

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string fileName = saveFileDialog1.FileName;
                WriteToTxt(fileName);
            }
            else
            {
                MessageBox.Show("取消操作！");
            }
        }
        #endregion

        #region 写入本地的txt中
        public static void WriteToTxt(string strFile)
        {
            using (FileStream fs = new FileStream(strFile, FileMode.CreateNew))
            {
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.WriteLine(msg);
                }
            }
            LogHelper.Info("保存成功！");
            MessageBox.Show("保存成功！");
        }
        #endregion
    }

}
