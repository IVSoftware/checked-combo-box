# Force repaint on ComboBox's DropDownList

I'm working with a `ComboBox` bound to a list of `CheckBox` controls to make a checked combo box list. The specific implementation is my own, but the idea came from an SO [question](https://stackoverflow.com/questions/75725304/force-repaint-on-comboboxs-dropdownlist) whose link seems to have gone dark for the time being. 

When the time comes to draw the item in the drop list, the approach I take is to just to render the checkbox into a bitmap, and draw the bitmap into the bounds provided by the `DrawItem` override. At the same time, I get the screen coordinates relative to the hWnd of the drop list and stuff the rectangle into the `Tag` property of the check box so that I can hit test it when the WM_LBUTTONDOWN hook intercepts the click.

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);
        CheckBox checkbox;
        checkbox = CheckBoxItems[e.Index];
        checkbox.Size = e.Bounds.Size;

        // PInvoke to get the screen coordinates relative to the drop list.
        POINT p = new POINT(e.Bounds.Left, e.Bounds.Top);
        ClientToScreen(_hwndList, ref p);
        checkbox.Tag = new Rectangle(
            new Point(p.X, p.Y),
            e.Bounds.Size);

        using (var bitmap = new Bitmap(e.Bounds.Width, e.Bounds.Height))
        {
            checkbox.DrawToBitmap(bitmap, new Rectangle(Point.Empty, e.Bounds.Size));
            e.Graphics.DrawImage(bitmap, e.Bounds);
        }
    }

The custom ComboBox class implements [IMessageFilter](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.imessagefilter). It compares the `MousePosition` in screen coordinates to the screen rectangle captured in the `Tag` property. There is also a primitive hit test, where the checkbox is considered toggled if the hit occurs in the first 30 units of offset.
- If the hit test is a checkbox toggle, the drop list remains open and the CheckBox control toggles state.
- If the hit test is to the right of the checkbox, the drop list closes and commits without making any more state changes to the checkboxes.

As the mouse moves over the drop list items, the `SelectedIndex` is changing internally. The `WM_MOUSEMOVE` keeps this value current.

    public bool PreFilterMessage(ref Message m)
    {
        if (m.HWnd.Equals(_hwndList))
        {
            switch (m.Msg)
            {
                case WM_MOUSEMOVE:
                    SelectedIndexInDropDown = SelectedIndex;
                    break;
                case WM_LBUTTONDOWN:
                    var capture = MousePosition;
                    var checkbox =
                        CheckBoxItems
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
                            updateText();
                            return true;
                        }
                    }
                    break;
            }
        }
        return false;
    }

While all of this works well, The same issue presents as in the original post. The "real" checkbox that is the backing store of the list item gets toggled but the visual on the screen is a bitmap that was captured in its former state. And the problem is that this bitmap isn't going to change just by invalidating that rectangle. So the question remains: How are we going to get that `CheckBox` to repaint in the new state?


# Proposed Solution

The mouse event hook is already keeping track of the item index as the mouse moves over the list. What we notice in fact is that when the internal selected index changes, the base `ComboBox` control is issuing the `DrawItem` command. So it's really close as it is: If you check the item, roll over a _different_ item and then roll back over the item you just checked, suddenly the checkbox is redrawn in the correct state.

This suggests that all one has to do is re-issue `CB_SETCURSEL` message for the item that was checked. In fact this does seem solve the question and draw the checkbox in the correct state immediatelyd.

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
                    var checkbox =
                        CheckBoxItems
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

                            // HERE 
                            SendMessage(Handle, CB_SETCURSEL, CheckBoxItems.IndexOf(checkbox), 0);

                            updateText();
                            return true;
                        }
                    }
                    break;
            }
        }
        return false;
    }
