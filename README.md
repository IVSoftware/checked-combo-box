# Force repaint on ComboBox's DropDownList

I have a `ComboBox` bound to a list of `CheckBox`. When it's time to draw the item, I just render the checkbox into a bitmap, and draw the bitmap into the bounds provided by the `DrawItem override.

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        base.OnDrawItem(e);
        CheckBox checkbox;
        checkbox = CheckBoxItems[e.Index];
        checkbox.Size = e.Bounds.Size;

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
