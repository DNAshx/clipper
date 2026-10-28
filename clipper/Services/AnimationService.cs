using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace clipper.Services
{
    public static class AnimationService
    {
        public static readonly TimeSpan SpeechAnimationDuration =
            TimeSpan.FromMilliseconds(250);

        public static void StartIdle(
            TranslateTransform translate,
            RotateTransform rotate,
            ScaleTransform scale)
        {
            DoubleAnimation moveAnimation = new()
            {
                From = 0,
                To = -4,
                Duration = TimeSpan.FromSeconds(1.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            DoubleAnimation rotateAnimation = new()
            {
                From = -1.5,
                To = 1.5,
                Duration = TimeSpan.FromSeconds(2.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            DoubleAnimation scaleAnimation = new()
            {
                From = 1.0,
                To = 1.015,
                Duration = TimeSpan.FromSeconds(1.8),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            translate.BeginAnimation(
                TranslateTransform.YProperty,
                moveAnimation);

            rotate.BeginAnimation(
                RotateTransform.AngleProperty,
                rotateAnimation);

            scale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                scaleAnimation);

            scale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                scaleAnimation);
        }

        public static void AnimateSpeechBubble(UIElement bubble)
        {
            DoubleAnimation fadeAnimation = new()
            {
                From = 0.25,
                To = 1.0,
                Duration = SpeechAnimationDuration
            };

            bubble.BeginAnimation(
                UIElement.OpacityProperty,
                fadeAnimation
            );
        }
        public static void React(TranslateTransform translate)
        {
            DoubleAnimation jumpAnimation = new()
            {
                From = 0,
                To = -18,
                Duration = TimeSpan.FromMilliseconds(120),
                AutoReverse = true
            };

            translate.BeginAnimation(
                TranslateTransform.YProperty,
                jumpAnimation,
                HandoffBehavior.Compose);
        }
    }
}