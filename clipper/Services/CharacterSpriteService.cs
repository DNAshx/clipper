using clipper.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace clipper.Services
{
    public sealed class CharacterSpriteService
    {
        private readonly Grid characterCanvas;
        private readonly Canvas eyesCanvas;
        private readonly Image baseImage;
        private readonly List<EyeView> eyes = new();
        private BitmapSource? idleImage;
        private BitmapSource? blinkImage;
        private BitmapSource? sleepImage;
        private bool sleeping;
        private bool blinking;

        public CharacterSpriteService(Grid characterCanvas, Canvas eyesCanvas, Image baseImage)
        {
            this.characterCanvas = characterCanvas;
            this.eyesCanvas = eyesCanvas;
            this.baseImage = baseImage;
        }

        public bool CanTrackCursor => eyes.Count > 0 && !sleeping && !blinking;

        public void SetSprites(ArchetypeSprites sprites)
        {
            // Загружаем всё до смены изображения, чтобы не оставить половину персонажа.
            var nextEyes = new List<EyeView>();
            BitmapSource nextIdle, nextBlink, nextSleep;
            Brush? nextMask = null;
            if (!string.IsNullOrWhiteSpace(sprites.Base))
            {
                var layers = sprites.Eyes ??
                    throw new InvalidDataException("Layered sprites require eyes configuration.");
                nextIdle = ImageService.Load(sprites.Base);
                nextBlink = nextSleep = nextIdle;
                var whites = layers.ProceduralWhites ? null : ImageService.Load(layers.Whites);
                var pupils = layers.ProceduralPupils ? null : ImageService.Load(layers.Pupils);
                var blink = layers.ProceduralLids ? null : ImageService.Load(layers.Blink);
                var sleep = layers.ProceduralLids ? null : ImageService.Load(layers.Sleep);
                foreach (var placement in new[] { layers.Left, layers.Right })
                {
                    if (!double.IsFinite(placement.X) || !double.IsFinite(placement.Y) ||
                        !double.IsFinite(placement.Width) || !double.IsFinite(placement.Height) ||
                        placement.X < 0 || placement.Y < 0 || placement.Width <= 0 || placement.Height <= 0 ||
                        placement.X + placement.Width > nextIdle.PixelWidth ||
                        placement.Y + placement.Height > nextIdle.PixelHeight ||
                        !double.IsFinite(placement.PupilSize) || placement.PupilSize <= 0 ||
                        placement.PupilSize >= Math.Min(placement.Width, placement.Height) ||
                        !double.IsFinite(placement.MaxPupilOffset) || placement.MaxPupilOffset < 0 ||
                        !double.IsFinite(placement.LidHeight) || placement.LidHeight <= 0)
                        throw new InvalidDataException("Invalid eye placement.");
                    nextEyes.Add(new EyeView(placement,
                        whites == null ? CreateWhites() : Crop(whites, placement.Whites),
                        pupils == null ? CreatePupils() : Crop(pupils, placement.Pupils),
                        blink == null ? CreateLids(layers.LidColor, false) : Crop(blink, placement.Blink),
                        sleep == null ? CreateLids(layers.LidColor, true) : Crop(sleep, placement.Sleep)));
                }
                if (layers.MaskBaseEyes)
                    nextMask = CreateBaseMask(nextIdle, layers);
            }
            else
            {
                nextIdle = ImageService.Load(sprites.Idle);
                nextBlink = ImageService.Load(sprites.Blink);
                nextSleep = ImageService.Load(sprites.Sleep);
            }

            eyesCanvas.Children.Clear();
            eyes.Clear();
            eyes.AddRange(nextEyes);
            foreach (var eye in eyes)
                eyesCanvas.Children.Add(eye.Canvas);
            baseImage.OpacityMask = nextMask;
            idleImage = nextIdle;
            blinkImage = nextBlink;
            sleepImage = nextSleep;
            characterCanvas.Width = nextIdle.PixelWidth;
            characterCanvas.Height = nextIdle.PixelHeight;
            sleeping = blinking = false;
            Render();
        }

        public void SetSleeping(bool value)
        {
            sleeping = value;
            blinking = false;
            Render();
        }

        public void SetBlinking(bool value)
        {
            blinking = value && !sleeping;
            Render();
        }

        public void UpdateGaze(Point cursor)
        {
            if (!CanTrackCursor) return;
            foreach (var eye in eyes)
                eye.UpdateGaze(cursor);
        }

        private void Render()
        {
            baseImage.Source = eyes.Count > 0
                ? idleImage
                : sleeping ? sleepImage : blinking ? blinkImage : idleImage;
            foreach (var eye in eyes)
                eye.Render(sleeping, blinking);
        }

        private static ImageSource CreateWhites()
        {
            var shading = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.4, 0.3),
                RadiusX = 0.6,
                RadiusY = 0.6
            };
            shading.GradientStops.Add(new GradientStop(Colors.White, 0));
            shading.GradientStops.Add(new GradientStop(Color.FromRgb(248, 248, 248), 0.5));
            shading.GradientStops.Add(new GradientStop(Color.FromRgb(186, 186, 186), 0.85));
            shading.GradientStops.Add(new GradientStop(Color.FromRgb(104, 104, 104), 1));
            var image = new DrawingImage(new GeometryDrawing(shading, null,
                new RectangleGeometry(new Rect(0, 0, 1, 1))));
            image.Freeze();
            return image;
        }

        private static Geometry CreateAperture(EyePlacement placement)
        {
            return string.IsNullOrWhiteSpace(placement.Aperture)
                ? new EllipseGeometry(new Rect(0, 0, placement.Width, placement.Height))
                : Geometry.Parse(placement.Aperture);
        }

        private static ImageSource CreatePupils()
        {
            var image = new DrawingImage(new GeometryDrawing(Brushes.Black, null,
                new EllipseGeometry(new Rect(0, 0, 1, 1))));
            image.Freeze();
            return image;
        }

        private static ImageSource CreateLids(string color, bool sleeping)
        {
            var lidColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            var fill = new RadialGradientBrush(Colors.LightGray, lidColor)
            {
                GradientOrigin = new Point(0.35, 0.2),
                RadiusX = 0.75,
                RadiusY = 0.75
            };
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(fill, null,
                new RectangleGeometry(new Rect(0, 0, 1, 1))));
            drawing.Children.Add(new GeometryDrawing(null, new System.Windows.Media.Pen(Brushes.Black, 0.018),
                Geometry.Parse(sleeping ? "M 0,0.5 Q 0.5,0.7 1,0.55" : "M 0,0.6 Q 0.5,0.4 1,0.65")));
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        private static Brush CreateBaseMask(BitmapSource source, EyeLayers layers)
        {
            var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometry.Children.Add(new RectangleGeometry(new Rect(0, 0, source.PixelWidth, source.PixelHeight)));
            foreach (var placement in new[] { layers.Left, layers.Right })
            {
                var aperture = CreateAperture(placement).Clone();
                aperture.Transform = new TranslateTransform(placement.X, placement.Y);
                geometry.Children.Add(aperture);
            }
            var mask = new DrawingBrush(new GeometryDrawing(Brushes.White, null, geometry))
            {
                Stretch = Stretch.Fill
            };
            mask.Freeze();
            return mask;
        }

        private static BitmapSource Crop(BitmapSource source, SpriteCrop crop)
        {
            if (crop.X < 0 || crop.Y < 0 || crop.Width <= 0 || crop.Height <= 0 ||
                (long)crop.X + crop.Width > source.PixelWidth ||
                (long)crop.Y + crop.Height > source.PixelHeight)
                throw new InvalidDataException("Eye crop is outside its PNG.");
            var image = new CroppedBitmap(source,
                new Int32Rect(crop.X, crop.Y, crop.Width, crop.Height));
            image.Freeze();
            return image;
        }

        private sealed class EyeView
        {
            private readonly EyePlacement placement;
            private readonly Shape fill;
            private readonly Image whites;
            private readonly Image pupils;
            private readonly Image lids;
            private readonly ImageSource blink;
            private readonly ImageSource sleep;
            private readonly Brush openFill = Brushes.White;
            private readonly Brush closedFill = new LinearGradientBrush(
                Color.FromRgb(62, 61, 66), Color.FromRgb(18, 18, 21), 90);
            public Canvas Canvas { get; }

            public EyeView(EyePlacement placement, ImageSource whites, ImageSource pupils,
                ImageSource blink, ImageSource sleep)
            {
                this.placement = placement;
                this.blink = blink;
                this.sleep = sleep;
                Canvas = new Canvas
                {
                    Width = placement.Width,
                    Height = placement.Height,
                    IsHitTestVisible = false,
                    Clip = CreateAperture(placement)
                };
                System.Windows.Controls.Canvas.SetLeft(Canvas, placement.X);
                System.Windows.Controls.Canvas.SetTop(Canvas, placement.Y);
                fill = new System.Windows.Shapes.Rectangle { Width = placement.Width, Height = placement.Height };
                this.whites = new Image
                {
                    Source = whites, Width = placement.Width, Height = placement.Height, Stretch = Stretch.Fill
                };
                this.pupils = new Image
                {
                    Source = pupils, Width = placement.PupilSize, Height = placement.PupilSize,
                    Stretch = Stretch.Fill
                };
                lids = new Image
                {
                    Width = placement.Width, Height = placement.LidHeight, Stretch = Stretch.Fill
                };
                System.Windows.Controls.Canvas.SetTop(lids, (placement.Height - placement.LidHeight) / 2);
                Canvas.Children.Add(fill);
                Canvas.Children.Add(this.whites);
                Canvas.Children.Add(this.pupils);
                Canvas.Children.Add(lids);
                CenterPupils();
            }

            public void Render(bool sleeping, bool blinking)
            {
                bool closed = sleeping || blinking;
                fill.Fill = closed ? closedFill : openFill;
                whites.Visibility = pupils.Visibility = closed ? Visibility.Hidden : Visibility.Visible;
                lids.Visibility = closed ? Visibility.Visible : Visibility.Hidden;
                lids.Source = sleeping ? sleep : blink;
                if (closed) CenterPupils();
            }

            private void CenterPupils()
            {
                System.Windows.Controls.Canvas.SetLeft(pupils, (placement.Width - placement.PupilSize) / 2);
                System.Windows.Controls.Canvas.SetTop(pupils, (placement.Height - placement.PupilSize) / 2);
            }

            public void UpdateGaze(Point cursor)
            {
                double dx = cursor.X - placement.X - placement.Width / 2;
                double dy = cursor.Y - placement.Y - placement.Height / 2;
                if (!double.IsFinite(dx) || !double.IsFinite(dy)) return;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double maxOffset = Math.Min(placement.MaxPupilOffset,
                    (Math.Min(placement.Width, placement.Height) - placement.PupilSize) / 2);
                double factor = maxOffset / Math.Max(length, 180);
                System.Windows.Controls.Canvas.SetLeft(pupils, (placement.Width - placement.PupilSize) / 2 + dx * factor);
                System.Windows.Controls.Canvas.SetTop(pupils, (placement.Height - placement.PupilSize) / 2 + dy * factor);
            }
        }
    }
}