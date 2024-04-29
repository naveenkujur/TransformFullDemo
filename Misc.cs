using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace transform {
   static class Util {
      public static Matrix ComputeZoomExtentsProjXfm (double viewWidth, double viewHeight, Bound b) {
         var viewMargin = 0;
         // Compute the scaling, to fit specified drawing extents into the view space
         double scaleX = (viewWidth - 2 * viewMargin) / b.Width, scaleY = (viewHeight - 2 * viewMargin) / b.Height;
         double scale = Math.Min (scaleX, scaleY);
         var scaleMatrix = Matrix.Identity; scaleMatrix.Scale (scale, -scale);
         // translation...
         Point projectedMidPt = scaleMatrix.Transform (b.Mid);
         Point viewMidPt = new (viewWidth / 2, viewHeight / 2);
         var translateMatrix = Matrix.Identity; translateMatrix.Translate (viewMidPt.X - projectedMidPt.X, viewMidPt.Y - projectedMidPt.Y);
         // Final zoom extents matrix, is a product of scale and translate matrices
         scaleMatrix.Append (translateMatrix);
         return scaleMatrix;
      }
   }

   class PanWidget { // Works in screen space
      #region Constructors
      public PanWidget (UIElement eventSource, Action<Vector> onPan) {
         mOnPan = onPan;
         eventSource.MouseDown += (sender, e) => {
            if (e.ChangedButton == MouseButton.Middle) PanStart (e.GetPosition (eventSource));
         };
         eventSource.MouseUp += (sender, e) => {
            if (IsPanning) PanEnd (e.GetPosition (eventSource));
         };
         eventSource.MouseMove += (sender, e) => {
            if (IsPanning) PanMove (e.GetPosition (eventSource));
         };
         eventSource.MouseLeave += (sender, e) => {
            if (IsPanning) PanCancel ();
         };
      }
      #endregion

      #region Implementation
      bool IsPanning => mPrevPt != null;

      void PanStart (Point pt) {
         mPrevPt = pt;
      }

      void PanMove (Point pt) {
         mOnPan.Invoke (pt - mPrevPt!.Value);
         mPrevPt = pt;
      }

      void PanEnd (Point? pt) {
         if (pt.HasValue)
            PanMove (pt.Value);
         mPrevPt = null;
      }

      void PanCancel () => PanEnd (null);
      #endregion

      #region Private
      Point? mPrevPt;
      readonly Action<Vector> mOnPan;
      #endregion
   }

   class DrawingSurface : Canvas { // overrides OnRender
      #region Properties
      public Drawing? Drawing;
      public LineBuilder? LineBuilder;
      public Matrix Xfm;
      #endregion

      #region Overrides
      protected override void OnRender (DrawingContext dc) {
         base.OnRender (dc);
         if (LineBuilder is null) return;
         var dwgCmds = new DrawingCommands (dc, Xfm);
         // Draw feedback, if any
         LineBuilder!.Draw (dwgCmds);
         // Draw the drawing (or drawing entities)
         Drawing!.Draw (dwgCmds);

         // We need a list of drawables
      }
      #endregion
   }

   class DrawingCommands { // adapter wrapper on DrawingContext
      #region Constructors
      public DrawingCommands (DrawingContext dc, Matrix projXfm) => (mDc, mXfm) = (dc, projXfm);
      #endregion

      #region Methods
      public void DrawLines (IEnumerable<Point> dwgPts) {
         var itr = dwgPts.GetEnumerator ();
         if (!itr.MoveNext ()) return;
         var prevPt = itr.Current;
         while (itr.MoveNext ()) {
            DrawLine (prevPt, itr.Current);
            prevPt = itr.Current;
         }
      }

      public void DrawLine (Point startPt, Point endPt) {
         var pen = new Pen (Brushes.Black, 1);
         mDc.DrawLine (pen, mXfm.Transform (startPt), mXfm.Transform (endPt));
      }
      #endregion

      #region Private
      readonly DrawingContext mDc;
      readonly Matrix mXfm;
      #endregion
   }

   readonly struct Bound { // Bound in drawing space
      #region Constructors
      public Bound (Point cornerA, Point cornerB) {
         MinX = Math.Min (cornerA.X, cornerB.X);
         MaxX = Math.Max (cornerA.X, cornerB.X);
         MinY = Math.Min (cornerA.Y, cornerB.Y);
         MaxY = Math.Max (cornerA.Y, cornerB.Y);
      }

      public Bound (IEnumerable<Point> pts) {
         MinX = pts.Min (p => p.X);
         MaxX = pts.Max (p => p.X);
         MinY = pts.Min (p => p.Y);
         MaxY = pts.Max (p => p.Y);
      }

      public Bound (IEnumerable<Bound> bounds) {
         MinX = bounds.Min (b => b.MinX);
         MaxX = bounds.Max (b => b.MaxX);
         MinY = bounds.Min (b => b.MinY);
         MaxY = bounds.Max (b => b.MaxY);
      }

      public Bound () {
         this = Empty;
      }

      public static readonly Bound Empty = new () { MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue };
      #endregion

      #region Properties
      public double MinX { get; init; }
      public double MaxX { get; init; }
      public double MinY { get; init; }
      public double MaxY { get; init; }
      public double Width => MaxX - MinX;
      public double Height => MaxY - MinY;
      public Point Mid => new ((MaxX + MinX) / 2, (MaxY + MinY) / 2);
      public bool IsEmpty => MinX > MaxX || MinY > MaxY;
      #endregion

      #region Methods
      public Bound Inflated (Point ptAt, double factor) {
         if (IsEmpty) return this;
         var minX = ptAt.X - (ptAt.X - MinX) * factor;
         var maxX = ptAt.X + (MaxX - ptAt.X) * factor;
         var minY = ptAt.Y - (ptAt.Y - MinY) * factor;
         var maxY = ptAt.Y + (MaxY - ptAt.Y) * factor;
         return new () { MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY };
      }
      #endregion
   }

   interface IDrawable {
      Action RedrawReq { get; set; }
      void Draw (DrawingCommands drawingCommands);
   }

   abstract class DrawableBase : IDrawable {
      public Action RedrawReq { get; set; } = delegate { };

      public abstract void Draw (DrawingCommands drawingCommands);
   }

   class LineBuilder : DrawableBase { // in drawing space
      #region Methods
      public Pline? PointClicked (Point drawingPt) {
         if (mFirstPt is null) {
            mFirstPt = drawingPt;
            return null;
         } else {
            var firstPt = mFirstPt.Value;
            mFirstPt = null;
            return Pline.CreateLine (firstPt, drawingPt);
         }
      }

      public void PointHover (Point drawingPt) {
         mHoverPt = drawingPt;
         RedrawReq ();
      }
      #endregion

      #region DrawableBase implementation
      public override void Draw (DrawingCommands drawingCommands) {
         if (mFirstPt == null || mHoverPt == null) return;
         drawingCommands.DrawLine (mFirstPt.Value, mHoverPt.Value);
      }
      #endregion

      #region Private
      Point? mFirstPt;
      Point? mHoverPt;
      #endregion
   }

   class Drawing : DrawableBase {
      #region Methods
      public void AddPline (Pline pline) {
         mPlines.Add (pline);
         Bound = new Bound (mPlines.Select (pline => pline.Bound));
         RedrawReq ();
      }
      #endregion

      #region Properties
      public Bound Bound { get; private set; }
      #endregion

      #region DrawableBase implementation
      public override void Draw (DrawingCommands drawingCommands) {
         foreach (var pline in mPlines)
            drawingCommands.DrawLines (pline.GetPoints ());
      }
      #endregion

      #region Private
      readonly List<Pline> mPlines = [];
      #endregion
   }

   class Pline {
      #region Constructors
      public Pline (IEnumerable<Point> pts) => (mPoints, Bound) = (pts.ToList (), new Bound (pts));

      public static Pline CreateLine (Point startPt, Point endPt) {
         return new Pline (Enum (startPt, endPt));

         // local
         static IEnumerable<Point> Enum (Point a, Point b) {
            yield return a;
            yield return b;
         }
      }

      public static Pline CreateRectangle (Point startCornerPt, Point endCornerPoint) { throw new NotImplementedException (); }

      public static Pline CreateCenterRectangle (Point center, Point cornerPoint) { throw new NotImplementedException (); }
      #endregion

      #region Properties
      public Bound Bound { get; }
      public IEnumerable<Point> GetPoints () => mPoints;
      #endregion

      #region Private
      readonly List<Point> mPoints = [];
      #endregion
   }
}
