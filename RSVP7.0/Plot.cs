using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace RSVP7._0
{
    class Plot
    {
        public Plot(Graphics gh, Point center, int pixLength, Pen pen)
        {
            g = gh;
            pCenter = center;
            reallength = (pixLength / 10) * 10;
            linePen = pen;
        }

        public void Plotaxis()
        {
            //Graphics g = this.CreateGraphics();
            //g.Clear(Color.White);
            Font font = new Font("Times New Roman", 11);
            SolidBrush brush = new SolidBrush(Color.DarkBlue);
            Pen pen = new Pen(Color.DarkBlue);
            //pen.EndCap = LineCap.ArrowAnchor;    //是线的尽头是arrow
            pen.DashStyle = DashStyle.Solid;
            //坐标轴
            g.DrawLine(pen, pCenter, new PointF(pCenter.X + reallength, pCenter.Y));//x
            g.DrawLine(pen, pCenter, new PointF(pCenter.X, pCenter.Y - reallength));//y      
            g.DrawLine(pen, new PointF(pCenter.X, pCenter.Y - reallength), new PointF(pCenter.X + reallength, pCenter.Y - reallength));
            g.DrawLine(pen, new PointF(pCenter.X + reallength, pCenter.Y), new PointF(pCenter.X + reallength, pCenter.Y - reallength));
            pen.DashStyle = DashStyle.Dash;
            g.DrawLine(pen, pCenter, new PointF(pCenter.X + reallength, pCenter.Y - reallength));

            //轴标格
            int iX = reallength / 10;
            for (int i = 1; i <= 10; i++)
            {
                // 偶数标长度长，而且标明刻度
                g.DrawLine(Pens.Black, new PointF(pCenter.X + iX * i, pCenter.Y), new PointF(pCenter.X + iX * i, pCenter.Y - (2 - (i & 1)) * 4));//x
                if (0 == (i & 1))
                {
                    SizeF size = g.MeasureString((i * 0.1).ToString(), font);
                    g.DrawString((i * 0.1).ToString(), font, brush, new PointF(pCenter.X + iX * i - size.Width / 2, pCenter.Y));
                }

                g.DrawLine(Pens.Black, new PointF(pCenter.X, pCenter.Y - iX * i), new PointF(pCenter.X + (2 - (i & 1)) * 4, pCenter.Y - iX * i));//y
                if (0 == (i & 1))
                {
                    SizeF size = g.MeasureString((i * 0.1).ToString(), font);
                    g.DrawString((i * 0.1).ToString(), font, brush, new PointF(pCenter.X - size.Width, pCenter.Y - iX * i - size.Height / 2));
                }
            }

            // caption for the axis

            //g.DrawString("x", font, brush, new PointF(pCenter.X + reallength, pCenter.Y));
            //g.DrawString("y", font, brush, new PointF(pCenter.X, pCenter.Y - reallength));
            g.DrawString("0", font, brush, new PointF(pCenter.X, pCenter.Y));
            SizeF sz = g.MeasureString("0", font);
            g.DrawString("0", font, brush, new PointF(pCenter.X - sz.Width, pCenter.Y - font.Height));

            // title
            sz = g.MeasureString("", font);
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Far;
            sf.FormatFlags = StringFormatFlags.DirectionVertical;
            sz = g.MeasureString("True positive rate", font);
            g.DrawString("True positive rate", font, brush, pCenter.X - 40, pCenter.Y - (reallength - sz.Width) / 2, sf);
            sz = g.MeasureString("False positive rate", font);
            g.DrawString("False positive rate", font, brush, pCenter.X + (reallength - sz.Width) / 2, pCenter.Y + 20);

        }

        public float PlotRoc(Foo[] org, int size, int posLabel)
        {
            // pos 为正类标签
            PointF[] roc = new PointF[2 + size];
            float Nnum = 0;
            float Pnum = 0;
            int i = 0;
            for (i = 0; i < size; ++i)
            {
                if (posLabel == org[i].label)
                    ++Pnum;
                else
                    ++Nnum;
            }
            roc[0] = new PointF(0, 0);
            float fp = 0, tp = 0;           // fp: false positive; tp: true positive;
            for (i = 1; i < 1 + size; ++i)
            {
                if (org[i - 1].label == posLabel)
                {
                    tp += 1;
                }
                else
                {
                    fp += 1;
                }
                roc[i].X = fp / Nnum;
                roc[i].Y = tp / Pnum;
            }

            roc[1 + size].X = fp / Nnum;
            roc[1 + size].Y = tp / Pnum;

            float auc = 0;
            size += 2;
            for (i = 0; i < size - 1; ++i)
            {
                if (roc[i + 1].X != roc[i].X)
                {
                    auc += roc[i].Y;
                }
                //g.FillEllipse(lineBrush, new RectangleF(GetPoint(roc[i]), new SizeF(dotSize, dotSize))); // 画点
                g.DrawLine(linePen, GetPoint(roc[i]), GetPoint(roc[i + 1]));
            }

            auc *= 1 / Nnum;
            return auc;
        }

        private float trapezoid_area(float x1, float x2, float y1, float y2)
        {
            float Base = Math.Abs(x1 - x2);
            float height = (y1 + y2) / 2;
            return Base * height;
        }
        private PointF GetPoint(PointF p)
        {
            p.X *= reallength;
            p.Y *= reallength;
            p.X += pCenter.X;
            p.Y = pCenter.Y - p.Y;
            return p;
        }

        // member variable
        Graphics g;
        int reallength;      // length in pixel of aix
        PointF pCenter;
        Pen linePen;
    }
}
