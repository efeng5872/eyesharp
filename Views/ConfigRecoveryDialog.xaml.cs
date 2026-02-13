using System.Windows;

namespace eyesharp.Views
{
    /// <summary>
    /// 配置恢复对话框
    /// </summary>
    public partial class ConfigRecoveryDialog : Window
    {
        public ConfigRecoveryDialog()
        {
            InitializeComponent();

            // 禁用关闭按钮，必须点击确定
            this.Closing += (s, e) =>
            {
                e.Cancel = true;
            };
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
