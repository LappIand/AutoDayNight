using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AutoDayNight
{
    internal sealed class LocationForm : Form
    {
        private sealed class Place
        {
            public string Name, Zone; public decimal Lat, Lon;
            public override string ToString() { return Name; }
            public Place(string name, decimal lat, decimal lon, string zone) { Name = name; Lat = lat; Lon = lon; Zone = zone; }
        }
        private sealed class ZoneItem
        {
            public string Id, Name;
            public override string ToString() { return Name; }
            public ZoneItem(string id, string name) { Id = id; Name = name; }
        }
        private readonly ComboBox _places, _zones;
        private readonly TextBox _city;
        private readonly NumericUpDown _lat, _lon;
        private readonly Label _preview;
        private readonly Timer _previewTimer;

        public LocationForm()
        {
            Text = Texts.Get("所在地设置 — AutoDayNight", "Location — AutoDayNight");
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(600, 465); Font = new Font("Segoe UI", 10);
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);
            Label intro = new Label { Text = Texts.Get("选择当地城市或填写经纬度。每日离线计算，无定位上传。", "Choose your city or enter coordinates. Calculations stay offline."), AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(intro, 0, 0); layout.SetColumnSpan(intro, 2);
            _places = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _places.Items.Add(Texts.Get("自定义经纬度", "Custom coordinates"));
            Place[] cities = new Place[] {
                new Place("石家庄 / Shijiazhuang",38.0406m,114.5149m,"China Standard Time"),
                new Place("北京 / Beijing",39.9042m,116.4074m,"China Standard Time"),
                new Place("上海 / Shanghai",31.2304m,121.4737m,"China Standard Time"),
                new Place("广州 / Guangzhou",23.1291m,113.2644m,"China Standard Time"),
                new Place("深圳 / Shenzhen",22.5431m,114.0579m,"China Standard Time"),
                new Place("成都 / Chengdu",30.5728m,104.0668m,"China Standard Time"),
                new Place("乌鲁木齐 / Urumqi",43.8256m,87.6168m,"China Standard Time"),
                new Place("香港 / Hong Kong",22.3193m,114.1694m,"China Standard Time"),
                new Place("台北 / Taipei",25.0330m,121.5654m,"Taipei Standard Time"),
                new Place("Tokyo",35.6762m,139.6503m,"Tokyo Standard Time"),
                new Place("Singapore",1.3521m,103.8198m,"Singapore Standard Time"),
                new Place("New Delhi",28.6139m,77.2090m,"India Standard Time"),
                new Place("London",51.5074m,-0.1278m,"GMT Standard Time"),
                new Place("Berlin",52.5200m,13.4050m,"W. Europe Standard Time"),
                new Place("New York",40.7128m,-74.0060m,"Eastern Standard Time"),
                new Place("Los Angeles",34.0522m,-118.2437m,"Pacific Standard Time"),
                new Place("Sydney",-33.8688m,151.2093m,"AUS Eastern Standard Time"),
                new Place("Auckland",-36.8485m,174.7633m,"New Zealand Standard Time"),
                new Place("Honolulu",21.3099m,-157.8581m,"Hawaiian Standard Time"),
                new Place("Tromso",69.6492m,18.9553m,"W. Europe Standard Time") };
            foreach (Place p in cities) _places.Items.Add(p);
            _city = new TextBox { Dock = DockStyle.Fill, MaxLength = 80, Text = Config.City };
            _lat = Number(-90, 90, Config.Lat); _lon = Number(-180, 180, Config.Lon);
            _zones = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _zones.Items.Add(new ZoneItem("", Texts.Get("跟随 Windows 时区", "Follow Windows time zone") + " — " + TimeZoneInfo.Local.DisplayName));
            foreach (TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones()) _zones.Items.Add(new ZoneItem(z.Id, z.DisplayName));
            _zones.SelectedIndex = 0;
            for (int i = 0; i < _zones.Items.Count; i++) if (((ZoneItem)_zones.Items[i]).Id == Config.TimeZoneId) _zones.SelectedIndex = i;
            AddRow(layout, 1, Texts.Get("城市预设", "City preset"), _places);
            AddRow(layout, 2, Texts.Get("地点名称", "Location name"), _city);
            AddRow(layout, 3, Texts.Get("纬度（北正南负）", "Latitude (N+, S-)"), _lat);
            AddRow(layout, 4, Texts.Get("经度（东正西负）", "Longitude (E+, W-)"), _lon);
            AddRow(layout, 5, Texts.Get("时区 / 夏令时", "Time zone / DST"), _zones);
            _preview = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            layout.Controls.Add(_preview, 0, 6); layout.SetColumnSpan(_preview, 2);
            FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
            Button save = new Button { Text = Texts.Get("保存并应用", "Save and apply"), AutoSize = true };
            Button cancel = new Button { Text = Texts.Get("取消", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 7); layout.SetColumnSpan(buttons, 2);
            AcceptButton = save; CancelButton = cancel;
            _previewTimer = new Timer { Interval = 250 };
            _previewTimer.Tick += delegate { _previewTimer.Stop(); UpdatePreview(); };
            _places.SelectedIndex = 0;
            _places.SelectedIndexChanged += delegate {
                Place p = _places.SelectedItem as Place;
                if (p == null) return;
                _city.Text = p.Name; _lat.Value = p.Lat; _lon.Value = p.Lon;
                for (int i = 0; i < _zones.Items.Count; i++) if (((ZoneItem)_zones.Items[i]).Id == p.Zone) _zones.SelectedIndex = i;
                QueuePreview();
            };
            _lat.ValueChanged += delegate { QueuePreview(); }; _lon.ValueChanged += delegate { QueuePreview(); };
            _zones.SelectedIndexChanged += delegate { QueuePreview(); };
            save.Click += Save;
            Shown += delegate { UpdatePreview(); };
            FormClosed += delegate { _previewTimer.Dispose(); };
        }
        private static NumericUpDown Number(decimal min, decimal max, double value)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = 4, Increment = 0.01m,
                Value = (decimal)Math.Max((double)min, Math.Min((double)max, double.IsNaN(value) || double.IsInfinity(value) ? 0 : value)), Dock = DockStyle.Fill };
        }
        private static void AddRow(TableLayoutPanel panel, int row, string label, Control field)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) }, 0, row);
            panel.Controls.Add(field, 1, row);
        }
        private TimeZoneInfo SelectedZone()
        { string id = ((ZoneItem)_zones.SelectedItem).Id; return id.Length == 0 ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(id); }
        private void QueuePreview() { _previewTimer.Stop(); _previewTimer.Start(); }
        private void UpdatePreview()
        {
            try
            {
                TimeZoneInfo zone = SelectedZone(); DateTime date = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
                SolarDay d = SolarSchedule.GetDay(date, (double)_lat.Value, (double)_lon.Value, zone);
                string rise = d.Sunrise.HasValue ? d.Sunrise.Value.ToString("HH:mm") : "—";
                string set = d.Sunset.HasValue ? d.Sunset.Value.ToString("HH:mm") : "—";
                _preview.Text = date.ToString("yyyy-MM-dd") + "  " + Texts.Get("日出 ", "Sunrise ") + rise + "   " + Texts.Get("日落 ", "Sunset ") + set
                    + (d.Kind == SolarDayKind.PolarDay ? Texts.Get("（极昼）", " (polar day)") : d.Kind == SolarDayKind.PolarNight ? Texts.Get("（极夜）", " (polar night)") : "")
                    + "\n" + Texts.Get("时间按所选时区显示。移动城市后请重新设置所在地。", "Times use the selected zone. Update your location when you move.");
            }
            catch (Exception ex) { _preview.Text = ex.Message; }
        }
        private void Save(object sender, EventArgs e)
        {
            if (_city.Text.Trim().Length == 0) { MessageBox.Show(Texts.Get("请填写地点名称。", "Enter a location name.")); return; }
            double oldLat = Config.Lat, oldLon = Config.Lon;
            string oldCity = Config.City, oldZone = Config.TimeZoneId; bool oldConfigured = Config.LocationConfigured;
            Config.City = _city.Text.Trim().Replace("#", ""); Config.Lat = (double)_lat.Value; Config.Lon = (double)_lon.Value;
            Config.TimeZoneId = ((ZoneItem)_zones.SelectedItem).Id; Config.LocationConfigured = true;
            if (!Config.Save())
            {
                Config.Lat = oldLat; Config.Lon = oldLon; Config.City = oldCity; Config.TimeZoneId = oldZone; Config.LocationConfigured = oldConfigured;
                MessageBox.Show(Texts.Get("配置保存失败，请检查目录写入权限。", "Configuration could not be saved. Check folder permissions.")); return;
            }
            DialogResult = DialogResult.OK; Close();
        }
    }
}
