using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DbDocGeneratorUI
{
    public partial class MainForm : Form
    {
#pragma warning disable CS8618
        private ComboBox cmbAuthType;
        private TextBox txtServer;
        private TextBox txtDatabase;
        private TextBox txtUser;
        private TextBox txtPassword;
        private ComboBox cmbFormat;
        private Button btnGenerate;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Button btnOpenFolder;
        private Label lblResult;
#pragma warning restore CS8618

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DbDoc Generator";
            this.Size = new System.Drawing.Size(420, 340);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 15, 20, 15),
                ColumnCount = 2,
                RowCount = 8,
                BackColor = System.Drawing.Color.Transparent
            };

            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Server
            mainPanel.Controls.Add(CreateLabel("Server:"), 0, 0);
            txtServer = CreateTextBox("");
            mainPanel.Controls.Add(txtServer, 1, 0);

            // Auth type
            mainPanel.Controls.Add(CreateLabel("Auth:"), 0, 1);
            cmbAuthType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 9),
                Height = 28
            };
            cmbAuthType.Items.Add("Windows Auth");
            cmbAuthType.Items.Add("SQL Auth");
            cmbAuthType.SelectedIndex = 0;
            cmbAuthType.SelectedIndexChanged += (s, e) =>
            {
                var isSqlAuth = cmbAuthType.SelectedIndex == 1;
                txtUser.Enabled = isSqlAuth;
                txtPassword.Enabled = isSqlAuth;
            };
            mainPanel.Controls.Add(cmbAuthType, 1, 1);

            // User
            mainPanel.Controls.Add(CreateLabel("User:"), 0, 2);
            txtUser = CreateTextBox("sa");
            txtUser.Enabled = false;
            mainPanel.Controls.Add(txtUser, 1, 2);

            // Password
            mainPanel.Controls.Add(CreateLabel("Password:"), 0, 3);
            txtPassword = CreateTextBox("");
            txtPassword.PasswordChar = '*';
            txtPassword.Enabled = false;
            mainPanel.Controls.Add(txtPassword, 1, 3);

            // Database
            mainPanel.Controls.Add(CreateLabel("Database:"), 0, 4);
            txtDatabase = CreateTextBox("");
            mainPanel.Controls.Add(txtDatabase, 1, 4);

            // Format
            mainPanel.Controls.Add(CreateLabel("Format:"), 0, 5);
            cmbFormat = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 9),
                Height = 28
            };
            cmbFormat.Items.Add("HTML");
            cmbFormat.Items.Add("Markdown");
            cmbFormat.SelectedIndex = 0;
            mainPanel.Controls.Add(cmbFormat, 1, 5);

            // Generate button
            btnGenerate = new Button
            {
                Text = "Generate Documentation",
                BackColor = System.Drawing.Color.FromArgb(26, 35, 126),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 36,
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += BtnGenerate_Click;
            mainPanel.Controls.Add(btnGenerate, 0, 6);
            mainPanel.SetColumnSpan(btnGenerate, 2);

            // Status
            var statusPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown
            };

            progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Height = 20,
                Visible = false
            };

            lblStatus = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9),
                ForeColor = System.Drawing.Color.FromArgb(100, 100, 100)
            };

            lblResult = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(46, 125, 50)
            };

            btnOpenFolder = new Button
            {
                Text = "Open Folder",
                Visible = false,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(26, 35, 126),
                ForeColor = System.Drawing.Color.White,
                Height = 28
            };
            btnOpenFolder.FlatAppearance.BorderSize = 0;
            btnOpenFolder.Click += (s, e) =>
            {
                var path = Path.Combine(Application.StartupPath, "documentation");
                if (Directory.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", path);
            };

            statusPanel.Controls.Add(progressBar);
            statusPanel.Controls.Add(lblStatus);
            statusPanel.Controls.Add(lblResult);
            statusPanel.Controls.Add(btnOpenFolder);

            mainPanel.Controls.Add(statusPanel, 0, 7);
            mainPanel.SetColumnSpan(statusPanel, 2);

            this.Controls.Add(mainPanel);
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 9),
                ForeColor = System.Drawing.Color.FromArgb(60, 60, 60),
                Height = 28
            };
        }

        private TextBox CreateTextBox(string defaultValue)
        {
            return new TextBox
            {
                Text = defaultValue,
                Font = new System.Drawing.Font("Segoe UI", 9),
                Height = 28,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDatabase.Text))
            {
                MessageBox.Show("Enter database name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGenerate.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Connecting to database...";
            lblResult.Text = "";
            btnOpenFolder.Visible = false;

            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = txtServer.Text,
                    InitialCatalog = txtDatabase.Text,
                    PersistSecurityInfo = true,
                    IntegratedSecurity = cmbAuthType.SelectedIndex == 0
                };

                if (!builder.IntegratedSecurity)
                {
                    builder.UserID = txtUser.Text;
                    builder.Password = txtPassword.Text;
                }

                var connStr = builder.ConnectionString;
                var isHtml = cmbFormat.SelectedIndex == 0;

                lblStatus.Text = "Reading database schema...";

                var schemaReader = new SchemaReader(connStr);
                var schema = schemaReader.ReadSchema();

                lblStatus.Text = $"Generating {(isHtml ? "HTML" : "Markdown")}...";

                string content;
                if (isHtml)
                {
                    var htmlGen = new HtmlGenerator();
                    content = htmlGen.Generate(schema, "dbo");
                }
                else
                {
                    var mdGen = new MarkdownGenerator();
                    content = mdGen.Generate(schema, "dbo");
                }

                var outputDir = Path.Combine(Application.StartupPath, "documentation");
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var fileName = isHtml ? "database_documentation.html" : "database_documentation.md";
                var filePath = Path.Combine(outputDir, fileName);

                File.WriteAllText(filePath, content, Encoding.UTF8);

                lblStatus.Text = "Done!";
                lblResult.Text = $"File: {fileName}";
                btnOpenFolder.Visible = true;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error";
                lblResult.ForeColor = System.Drawing.Color.FromArgb(198, 40, 40);
                lblResult.Text = ex.Message;
            }
            finally
            {
                progressBar.Visible = false;
                btnGenerate.Enabled = true;
            }
        }
    }
}