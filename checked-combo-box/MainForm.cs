using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace checked_combo_box
{
    public partial class MainForm : Form
    {
        public MainForm() =>InitializeComponent();
    }
    class ComboBoxEx : ComboBox, IMessageFilter
    {
        public ComboBoxEx()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DoubleBuffered= true;
            Application.AddMessageFilter(this);
            Disposed += (sender, e) =>Application.RemoveMessageFilter(this);

            DisplayMember = "Text";

            // Add items for testing purposes
            Items.Add(new CheckBox { Text = "Essential", BackColor = Color.White });
            Items.Add(new CheckBox { Text = "Primary", BackColor = Color.White });
            Items.Add(new CheckBox { Text = "Evoke", BackColor = Color.White });
            Items.Add(new CheckBox { Text = "Retain", BackColor = Color.White });
            Items.Add(new CheckBox { Text = "Model", BackColor = Color.White });
            Items.Add(new CheckBox { Text = "Personality", BackColor = Color.White });
        }
        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            // Get the handle of the listbox
            var mi = typeof(ComboBox).GetMethod("GetListHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var aspirant = (IntPtr)mi?.Invoke(this, new object[] { });
            _hwndList = aspirant;
        }
        public bool PreFilterMessage(ref Message m)
        {
            // Debug.WriteLine(m);

            if (m.HWnd.Equals(_hwndList))
            {
                switch (m.Msg)
                {
                    case WM_MOUSEMOVE:
                        SelectedIndexInDropDown = SelectedIndex;
                        break;
                    case WM_LBUTTONDOWN:
                        var capture = MousePosition;
                        var checkBoxItems = Items
                            .Cast<CheckBox>()
                            .ToList();
                        var checkbox =
                            checkBoxItems
                            .FirstOrDefault(_ => ((Rectangle)_.Tag).Contains(capture));
                        if (checkbox != null)
                        {
                            var rect = (Rectangle)checkbox.Tag;
                            var delta = capture.X - rect.Left;
                            // Hit test.
                            // - Keep open if checkbox toggled.
                            // - Otherwise close without toggling.
                            if (delta < 30)
                            {
                                checkbox.Checked = !checkbox.Checked;
                                SendMessage(Handle, CB_SETCURSEL, checkBoxItems.IndexOf(checkbox), 0);
                                updateText();
                                return true;
                            }
                        }
                        break;
                }
            }
            return false;
        }


        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if(IsHandleCreated) BeginInvoke(new Action(()=> updateText()));
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            base.OnDrawItem(e);
            var checkbox = (CheckBox)Items[e.Index];
            checkbox.Size = new Size(e.Bounds.Width - 10, e.Bounds.Height);

            POINT p = new POINT(e.Bounds.Left, e.Bounds.Top);
            ClientToScreen(_hwndList, ref p);
            checkbox.Tag = new Rectangle(
                new Point(p.X, p.Y),
                e.Bounds.Size);
            
            e.Graphics.FillRectangle(e.State.Equals(DrawItemState.Selected) ? Brushes.CornflowerBlue : Brushes.White, e.Bounds);
            using (var bitmap = new Bitmap(checkbox.Size.Width, checkbox.Size.Height))
            {
                checkbox.DrawToBitmap(bitmap, new Rectangle(Point.Empty, e.Bounds.Size));
                e.Graphics.DrawImage(bitmap, new Rectangle(new Point(e.Bounds.Left + 10, e.Bounds.Top), checkbox.Size));
            }
        }
        IntPtr _hwndList;
        // https://github.com/tpn/winsdk-10/blob/master/Include/10.0.10240.0/um/WinUser.h
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONDOWN = 0x0201;
        const int CB_SETCURSEL = 0x014E;

        int _internalSelectedIndex = -1;
        public int SelectedIndexInDropDown
        {
            get => _internalSelectedIndex;
            set
            {
                try
                {
                    if (!Equals(_internalSelectedIndex, value))
                    {
                        var checkBoxItems = Items.Cast<CheckBox>().ToArray();
                        Debug.WriteLine($"Internal selected index: {value}");
                        _internalSelectedIndex = value;
                        var textB4 = Text;
                        Text = string.Empty;
                        CheckBox checkbox;
                        for (int i = 0; i < Items.Count; i++)
                        {
                            if (i != _internalSelectedIndex)
                            {
                                checkbox = checkBoxItems[i];
                                if (checkbox.ForeColor == Color.White)
                                {
                                    checkbox.BackColor = Color.White;
                                    checkbox.ForeColor = SystemColors.ControlText;
                                    SendMessage(Handle, CB_SETCURSEL, i, 0);
                                }
                            }
                        }
                        if (SelectedIndex != -1)
                        {
                            checkbox = checkBoxItems[_internalSelectedIndex];
                            if (checkbox.ForeColor != Color.White)
                            {
                                checkbox.BackColor = Color.CornflowerBlue;
                                checkbox.ForeColor = Color.White;
                                SendMessage(Handle, CB_SETCURSEL, SelectedIndexInDropDown, 0);
                            }
                        }
                        updateText();
                    }
                }
                catch { };
            }
        }
        private void updateText()
        {
            var checkedItems = Items.Cast<CheckBox>().Where(_ => _.Checked).ToArray();
            string preview;
            switch (checkedItems.Length)
            {
                case 0:
                    return;
                case 1:
                    preview = checkedItems[0].Text;
                    break;
                default:
                    preview = $"{checkedItems.Length} Selected";
                    break;
            }
            if (Text != preview)
            {
                Text = preview;
                BeginInvoke(new Action(() => SelectAll()));
            }
        }

        #region P I N V O K E
        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public static implicit operator System.Drawing.Point(POINT p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }
            public static implicit operator POINT(System.Drawing.Point p)
            {
                return new POINT(p.X, p.Y);
            }
            public override string ToString()
            {
                return $"X: {X}, Y: {Y}";
            }
        }
        #endregion P I N V O K E
    }
}
