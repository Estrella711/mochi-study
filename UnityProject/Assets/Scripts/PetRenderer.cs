using System;
using UnityEngine;

namespace MochiDay
{
    /// <summary>
    /// Editable, code-native companion art. All coordinates use a 240 x 210 canvas.
    /// Textures are made once; the drawing path does not create managed objects.
    /// Call Dispose from the owning MonoBehaviour's OnDestroy.
    /// </summary>
    public sealed class PetRenderer : IDisposable
    {
        Texture2D oval, pill, triangle, triangleDown, silver, silverBody, cream, earSilver;
        bool disposed;
        float drawScale = 1f, drawX, drawY;

        static readonly Color SilverEdge = Rgb(131, 141, 155);
        static readonly Color FurWhite = Rgb(246, 246, 237);
        static readonly Color InnerEar = Rgb(220, 165, 171);
        static readonly Color Pink = Rgb(233, 177, 177);
        static readonly Color Mint = Rgb(119, 164, 146);
        static readonly Color Lilac = Rgb(170, 160, 198);
        static readonly Color LilacLight = Rgb(211, 201, 228);
        static readonly Color Gold = Rgb(220, 177, 78);
        static readonly Color Caramel = Rgb(178, 128, 84);
        static readonly Color Outline = Rgb(71, 82, 91);

        public PetRenderer()
        {
            oval = MakeEllipse(Color.white, Color.white, 0);
            pill = MakePill();
            triangle = MakeTriangle(Color.white, Color.white);
            triangleDown = MakeTriangle(Color.white, Color.white, true);
            silver = MakeEllipse(Rgb(174, 183, 195), Rgb(244, 243, 234), 0.13f);
            silverBody = MakeEllipse(Rgb(181, 191, 201), Rgb(233, 233, 224), 0.12f);
            cream = MakeEllipse(Rgb(251, 247, 235), Rgb(239, 232, 215), 0.055f);
            earSilver = MakeTriangle(Rgb(151, 162, 179), Rgb(224, 225, 218));
        }

        /// <param name="kind">"cat" (also "silver-cat") or "bunny". Unknown kinds use the bunny.</param>
        /// <param name="hat">"none", "hat-beret", or "hat-crown".</param>
        /// <param name="outfit">"none" or "outfit-sweater".</param>
        /// <param name="accessory">"none", "accessory-bow", "accessory-glasses", or "accessory-satchel".</param>
        public void Draw(Rect area, string kind, string hat, string outfit,
            string accessory, bool happy, float time, Color ink)
        {
            if (disposed || area.width <= 0 || area.height <= 0) return;
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            try
            {
                // Keep GUI.matrix unchanged here: an IMGUI ScrollView/Group contributes
                // its own clipping transform. Composing a local translation into that
                // matrix would apply the clipping origin a second time. Instead map the
                // artist's source canvas directly into the caller's local GUI coordinates.
                drawScale = Mathf.Min(area.width / 240f, area.height / 210f);
                drawX = area.center.x - 120f * drawScale;
                drawY = area.center.y - 105f * drawScale;
                GUI.color = Color.white;
                // Small soft ground shadow remains still while the companion bounces.
                Ellipse(65, 191, 115, 12, new Color(.34f, .44f, .39f, .08f));
                Ellipse(79, 194, 88, 7, new Color(.34f, .44f, .39f, .08f));
                float breath = Mathf.Sin(time * 2.25f);
                float bounce = happy ? -Mathf.Abs(Mathf.Sin(time * 7.4f)) * 8f : breath * 1.6f;
                drawY += bounce * drawScale;
                bool cat = kind == "cat" || kind == "silver-cat" || kind == "silver" || kind == "silver_cat";
                bool blink = Mathf.Repeat(time + .45f, 4.9f) > 4.71f;
                if (happy) DrawSparkles(time);
                if (cat) DrawCat(hat, outfit, accessory, happy, blink, time, breath, ink);
                else DrawBunny(hat, outfit, accessory, happy, blink, time, breath, ink);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
                drawScale = 1f; drawX = drawY = 0f;
            }
        }

        void DrawCat(string hat, string outfit, string accessory, bool happy,
            bool blink, float time, float breath, Color ink)
        {
            // Overlapping soft segments form a plush curled tail behind the body.
            float swish = Mathf.Sin(time * 1.55f) * 4f;
            Rotate(new Rect(161, 156, 39, 23), -12f + swish, new Vector2(165, 166), silverBody, Color.white);
            EllipseTexture(184, 150, 35, 29, silverBody);
            EllipseTexture(201, 137, 25, 30, silverBody);
            EllipseTexture(196, 124, 29, 25, silverBody);
            EllipseTexture(184, 126, 24, 22, silverBody);
            Ellipse(192, 127, 12, 8, SilverEdge * new Color(1, 1, 1, .45f));
            Line(214, 147, 219, 154, 4, new Color(.52f, .57f, .64f, .35f));
            Line(203, 166, 207, 172, 4, new Color(.52f, .57f, .64f, .35f));

            EllipseTexture(73, 113, 96, 77 + breath, silverBody);
            Ellipse(91, 133, 60, 54, FurWhite);
            DrawOutfit(outfit);
            DrawSatchelStrap(accessory, false);

            // Rounded triangular ears sit just behind a broad British shorthair face.
            Rotate(new Rect(51, 27, 55, 64), -16, new Vector2(80, 67), earSilver, Color.white);
            Rotate(new Rect(136, 25, 55, 64), 17, new Vector2(163, 67), earSilver, Color.white);
            Rotate(new Rect(62, 38, 32, 42), -16, new Vector2(80, 67), triangle, InnerEar);
            Rotate(new Rect(147, 37, 32, 42), 17, new Vector2(163, 67), triangle, InnerEar);
            EllipseTexture(47, 47, 147, 117, silver);
            // Cheek tufts and the milky muzzle soften the silver gradient.
            EllipseTexture(44, 104, 45, 42, silver);
            EllipseTexture(153, 103, 45, 42, silver);
            Ellipse(61, 103, 50, 43, new Color(.97f, .97f, .94f, .92f));
            Ellipse(132, 103, 50, 43, new Color(.97f, .97f, .94f, .92f));
            Ellipse(94, 113, 56, 37, FurWhite);
            // Silver forehead markings are subtle, like the shaded coat rather than stripes.
            Rotate(new Rect(99, 52, 11, 19), -12, new Vector2(105, 58), oval, new Color(.52f, .57f, .65f, .20f));
            Ellipse(115, 49, 11, 21, new Color(.52f, .57f, .65f, .24f));
            Rotate(new Rect(130, 52, 11, 19), 12, new Vector2(135, 58), oval, new Color(.52f, .57f, .65f, .20f));
            Ellipse(63, 118, 23, 10, new Color(.91f, .66f, .66f, .47f));
            Ellipse(157, 118, 23, 10, new Color(.91f, .66f, .66f, .47f));
            DrawCatEye(78, 93, blink, happy, ink);
            DrawCatEye(141, 93, blink, happy, ink);
            // Whiskers fan out from the cream cheeks.
            Color whisker = new Color(.39f, .44f, .49f, .52f);
            Line(69, 121, 45, 116, 1.5f, whisker);
            Line(68, 129, 42, 130, 1.5f, whisker);
            Line(70, 136, 48, 143, 1.5f, whisker);
            Line(173, 121, 197, 116, 1.5f, whisker);
            Line(174, 129, 200, 130, 1.5f, whisker);
            Line(172, 136, 194, 143, 1.5f, whisker);
            Rotate(new Rect(115, 120, 14, 10), 180, new Vector2(122, 125), triangle, Rgb(202, 143, 151));
            Line(122, 128, 122, 133, 1.7f, ink);
            // Two tiny curves form the cat's smiling mouth.
            Arc(116, 133, 6, 4, 12, 168, 1.8f, ink);
            Arc(128, 133, 6, 4, 12, 168, 1.8f, ink);
            if (happy) Ellipse(116, 139, 12, 6, Pink);
            DrawPaws(true, outfit, happy, time);
            DrawAccessories(accessory, true, ink);
            DrawHat(hat, true);
        }

        void DrawBunny(string hat, string outfit, string accessory, bool happy,
            bool blink, float time, float breath, Color ink)
        {
            EllipseTexture(153, 150, 32, 31, cream);
            EllipseTexture(76, 115, 90, 74 + breath, cream);
            Ellipse(92, 136, 58, 48, Rgb(255, 252, 242));
            DrawOutfit(outfit);
            DrawSatchelStrap(accessory, true);
            Rotate(new Rect(68, 14, 31, 87), -12f, new Vector2(88, 93), cream, Color.white);
            Rotate(new Rect(143, 13, 31, 86), 11f, new Vector2(153, 91), cream, Color.white);
            Rotate(new Rect(76, 22, 15, 58), -12f, new Vector2(88, 93), oval, Rgb(236, 191, 185));
            Rotate(new Rect(151, 21, 15, 57), 11f, new Vector2(153, 91), oval, Rgb(236, 191, 185));
            EllipseTexture(51, 67, 140, 97, cream);
            Ellipse(70, 84, 102, 72, new Color(1, .99f, .96f, .8f));
            Ellipse(69, 119, 24, 11, new Color(.92f, .68f, .65f, .65f));
            Ellipse(149, 119, 24, 11, new Color(.92f, .68f, .65f, .65f));
            if (blink || happy)
            {
                Arc(93, 110, 7, 5, 194, 346, 2.5f, ink);
                Arc(149, 110, 7, 5, 194, 346, 2.5f, ink);
            }
            else
            {
                Ellipse(86, 102, 11, 15, ink);
                Ellipse(142, 102, 11, 15, ink);
                Ellipse(89, 104, 3.6f, 4, Color.white);
                Ellipse(145, 104, 3.6f, 4, Color.white);
            }
            Ellipse(116, 122, 11, 7, Pink);
            Line(121.5f, 127, 121.5f, 131, 1.7f, ink);
            Arc(116, 131, 5.5f, 4, 12, 168, 1.8f, ink);
            Arc(127, 131, 5.5f, 4, 12, 168, 1.8f, ink);
            if (happy) Ellipse(116, 138, 11, 5, Pink);
            DrawPaws(false, outfit, happy, time);
            DrawAccessories(accessory, false, ink);
            DrawHat(hat, false);
        }

        void DrawCatEye(float x, float y, bool blink, bool happy, Color ink)
        {
            if (blink || happy)
            {
                Arc(x + 10, y + 10, 10, 6, 194, 346, 2.4f, ink);
                return;
            }
            Ellipse(x - 2, y - 2, 27, 27, new Color(.38f, .42f, .48f, .20f));
            Ellipse(x, y, 23, 24, Rgb(70, 91, 92));
            Ellipse(x + 2, y + 2, 19, 20, Rgb(132, 173, 158));
            Ellipse(x + 4, y + 8, 15, 12, Rgb(163, 193, 157));
            Ellipse(x + 8, y + 3, 7, 16, Outline);
            Ellipse(x + 4, y + 3, 7, 6, Color.white);
            Ellipse(x + 15, y + 14, 3, 3, new Color(1, 1, 1, .9f));
        }

        void DrawOutfit(string outfit)
        {
            if (outfit != "outfit-sweater" && outfit != "sweater") return;
            Ellipse(75, 127, 93, 56, Lilac);
            Round(78, 149, 86, 30, Lilac);
            Ellipse(94, 131, 57, 22, LilacLight);
            Line(85, 172, 158, 172, 3, LilacLight);
            Line(89, 163, 89, 171, 1.5f, new Color(1, 1, 1, .23f));
            Line(99, 163, 99, 171, 1.5f, new Color(1, 1, 1, .23f));
            Line(144, 163, 144, 171, 1.5f, new Color(1, 1, 1, .23f));
            Line(154, 163, 154, 171, 1.5f, new Color(1, 1, 1, .23f));
            // Embroidered daisy on the sweater's chest.
            Ellipse(117, 154, 7, 4, Rgb(249, 241, 215));
            Ellipse(117, 162, 7, 4, Rgb(249, 241, 215));
            Ellipse(112, 156, 5, 7, Rgb(249, 241, 215));
            Ellipse(124, 156, 5, 7, Rgb(249, 241, 215));
            Ellipse(117, 157, 7, 7, Gold);
        }

        void DrawPaws(bool cat, string outfit, bool happy, float time)
        {
            Texture2D fur = cat ? silverBody : cream;
            bool dressed = outfit == "outfit-sweater" || outfit == "sweater";
            float armRaise = happy ? 17f + Mathf.Sin(time * 7.4f) * 4 : 0;
            Rotate(new Rect(62, 140, 29, 41), 16, new Vector2(78, 145), fur, Color.white);
            Rotate(new Rect(153, 140 - armRaise, 29, 41), happy ? -34 : -16,
                new Vector2(164, 145), fur, Color.white);
            if (dressed)
            {
                Rotate(new Rect(64, 140, 27, 18), 16, new Vector2(78, 145), oval, Lilac);
                Rotate(new Rect(153, 140 - armRaise, 27, 18), happy ? -34 : -16,
                    new Vector2(164, 145), oval, Lilac);
            }
            EllipseTexture(78, 178, 37, 19, fur);
            EllipseTexture(129, 178, 37, 19, fur);
            // Toe seams are deliberately low contrast.
            Color toes = cat ? new Color(.48f, .54f, .60f, .28f) : new Color(.65f, .60f, .53f, .26f);
            Line(89, 184, 89, 190, 1.2f, toes);
            Line(97, 184, 97, 190, 1.2f, toes);
            Line(140, 184, 140, 190, 1.2f, toes);
            Line(148, 184, 148, 190, 1.2f, toes);
            if (happy)
            {
                Ellipse(161, 159 - armRaise, 11, 9, new Color(.86f, .59f, .62f, .72f));
                Ellipse(157, 153 - armRaise, 4, 4, Pink);
                Ellipse(163, 150 - armRaise, 4, 4, Pink);
                Ellipse(169, 153 - armRaise, 4, 4, Pink);
            }
        }

        void DrawHat(string hat, bool cat)
        {
            float y = cat ? 37f : 60f;
            if (hat == "hat-beret" || hat == "beret")
            {
                Rotate(new Rect(82, y - 12, 84, 28), -9, new Vector2(124, y + 7), oval, Rgb(142, 184, 163));
                Rotate(new Rect(87, y + 3, 73, 13), -9, new Vector2(124, y + 7), oval, Mint);
                Rotate(new Rect(118, y - 16, 9, 14), 15, new Vector2(121, y - 6), pill, Mint);
                Ellipse(101, y - 7, 30, 5, new Color(1, 1, 1, .18f));
            }
            else if (hat == "hat-crown" || hat == "crown")
            {
                float crownY = y - 17;
                Round(97, crownY + 20, 51, 16, Gold);
                Rotate(new Rect(94, crownY + 1, 22, 30), -18, new Vector2(106, crownY + 26), triangle, Gold);
                Paint(new Rect(111, crownY - 5, 23, 36), triangle, Rgb(229, 186, 83));
                Rotate(new Rect(130, crownY + 1, 22, 30), 18, new Vector2(140, crownY + 26), triangle, Gold);
                Ellipse(119, crownY + 23, 8, 8, Rgb(142, 188, 172));
                Ellipse(99, crownY + 26, 5, 5, Rgb(246, 223, 161));
                Ellipse(141, crownY + 26, 5, 5, Rgb(246, 223, 161));
                Ellipse(103, crownY - 1, 6, 6, Rgb(243, 211, 133));
                Ellipse(120, crownY - 8, 6, 6, Rgb(243, 211, 133));
                Ellipse(140, crownY - 1, 6, 6, Rgb(243, 211, 133));
                Line(101, crownY + 33, 144, crownY + 33, 2, Rgb(184, 142, 54));
            }
        }

        void DrawSatchelStrap(string accessory, bool bunny)
        {
            if (accessory != "accessory-satchel" && accessory != "satchel") return;
            Line(97, bunny ? 144 : 143, 159, 180, 7, Caramel);
            Line(97, bunny ? 144 : 143, 159, 180, 2, Rgb(209, 167, 119));
        }

        void DrawAccessories(string accessory, bool cat, Color ink)
        {
            if (accessory == "accessory-bow" || accessory == "bow")
            {
                Color bow = Rgb(196, 119, 133);
                Rotate(new Rect(103, 144, 19, 15), 15, new Vector2(121, 151), oval, bow);
                Rotate(new Rect(122, 144, 19, 15), -15, new Vector2(122, 151), oval, bow);
                Rotate(new Rect(108, 153, 9, 18), 17, new Vector2(118, 153), triangle, Rgb(213, 144, 151));
                Rotate(new Rect(127, 153, 9, 18), -17, new Vector2(126, 153), triangle, Rgb(213, 144, 151));
                Ellipse(116, 146, 12, 11, Rgb(230, 166, 170));
                Ellipse(108, 146, 5, 3, new Color(1, 1, 1, .22f));
            }
            else if (accessory == "accessory-glasses" || accessory == "glasses")
            {
                float y = cat ? 92 : 98;
                Ring(88, y + 10, 20, 17, 2.4f, Outline);
                Ring(154, y + 10, 20, 17, 2.4f, Outline);
                Arc(121, y + 14, 14, 7, 190, 350, 2.4f, Outline);
                Line(66, y + 7, 58, y + 4, 2.3f, Outline);
                Line(176, y + 7, 184, y + 4, 2.3f, Outline);
                Line(78, y + 1, 81, y - 2, 1.3f, new Color(1, 1, 1, .70f));
                Line(144, y + 1, 147, y - 2, 1.3f, new Color(1, 1, 1, .70f));
            }
            else if (accessory == "accessory-satchel" || accessory == "satchel")
            {
                Round(144, 163, 40, 28, Rgb(158, 109, 74));
                Round(146, 161, 39, 26, Caramel);
                Round(145, 159, 40, 15, Rgb(197, 149, 101));
                Line(149, 177, 180, 177, 1.2f, new Color(.97f, .86f, .67f, .45f));
                Round(161, 169, 9, 7, Gold);
                Round(164, 170, 3, 5, Rgb(247, 218, 157));
            }
        }

        void DrawSparkles(float time)
        {
            float pulse = .7f + Mathf.Sin(time * 5) * .15f;
            Star(30, 71, 7, new Color(.89f, .72f, .34f, pulse));
            Star(211, 80, 6, new Color(.68f, .77f, .60f, pulse));
            Star(189, 26, 4, new Color(.83f, .64f, .69f, pulse));
            Ellipse(32, 104, 4, 4, new Color(.89f, .72f, .34f, pulse));
            Ellipse(206, 49, 4, 4, new Color(.83f, .64f, .69f, pulse));
        }

        void Star(float x, float y, float radius, Color color)
        {
            Ellipse(x - 1.5f, y - radius, 3, radius * 2, color);
            Ellipse(x - radius, y - 1.5f, radius * 2, 3, color);
            Ellipse(x - 2, y - 2, 4, 4, color);
        }

        void Ellipse(float x, float y, float width, float height, Color color)
        { Paint(new Rect(x, y, width, height), oval, color); }

        void EllipseTexture(float x, float y, float width, float height, Texture2D texture)
        { Paint(new Rect(x, y, width, height), texture, Color.white); }

        void Round(float x, float y, float width, float height, Color color)
        { Paint(new Rect(x, y, width, height), pill, color); }

        void Paint(Rect rect, Texture2D texture, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(MapRect(rect), texture, ScaleMode.StretchToFill, true);
        }

        Rect MapRect(Rect rect)
        {
            return new Rect(drawX + rect.x * drawScale, drawY + rect.y * drawScale,
                rect.width * drawScale, rect.height * drawScale);
        }

        void Rotate(Rect rect, float degrees, Vector2 pivot, Texture2D texture, Color color)
        {
            // IMGUI's matrix rotation changes both the transform and clipping origin
            // under a scaled ScrollView. Upright plush parts preserve their local
            // geometry in every container; the inverted nose has its own cached mask.
            // Animation is expressed in source coordinates, never through GUI.matrix.
            if (texture == triangle && Mathf.Abs(degrees) >= 179f) texture = triangleDown;
            Paint(rect, texture, color);
        }

        void Line(float x1, float y1, float x2, float y2, float width, Color color)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < .001f) return;
            if (Mathf.Abs(dy) < .001f)
            {
                Round(Mathf.Min(x1, x2) - width * .5f, y1 - width * .5f,
                    length + width, width, color);
                return;
            }
            if (Mathf.Abs(dx) < .001f)
            {
                Round(x1 - width * .5f, Mathf.Min(y1, y2) - width * .5f,
                    width, length + width, color);
                return;
            }
            // Connected cached discs form an anti-aliased capsule at any slope.
            // This stays inside the caller's GUI clip without rotating any matrix.
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(.85f, width * .65f)));
            for (int i = 0; i <= steps; i++)
            {
                float fraction = (float)i / steps;
                Ellipse(x1 + dx * fraction - width * .5f,
                    y1 + dy * fraction - width * .5f, width, width, color);
            }
        }

        void Ring(float cx, float cy, float rx, float ry, float width, Color color)
        { Arc(cx, cy, rx, ry, 0, 360, width, color, 32); }

        void Arc(float cx, float cy, float rx, float ry, float first, float last,
            float width, Color color, int segments = 12)
        {
            float angle = first * Mathf.Deg2Rad;
            float previousX = cx + Mathf.Cos(angle) * rx;
            float previousY = cy + Mathf.Sin(angle) * ry;
            for (int i = 1; i <= segments; i++)
            {
                angle = Mathf.Lerp(first, last, (float)i / segments) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(angle) * rx, y = cy + Mathf.Sin(angle) * ry;
                Line(previousX, previousY, x, y, width, color);
                previousX = x; previousY = y;
            }
        }

        static Color Rgb(int r, int g, int b)
        { return new Color(r / 255f, g / 255f, b / 255f, 1); }

        static Texture2D NewTexture(int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        static Texture2D MakeEllipse(Color top, Color bottom, float edgeShade)
        {
            const int size = 128;
            var texture = NewTexture(size, "Mochi procedural fur");
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f - size * .5f) / (size * .5f - 1);
                float ny = (y + .5f - size * .5f) / (size * .5f - 1);
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01((1 - distance) * 64f);
                float down = 1 - (y + .5f) / size;
                Color color = Color.Lerp(top, bottom, Mathf.SmoothStep(0, 1, down));
                float shade = 1 - edgeShade * Mathf.Pow(Mathf.Abs(nx), 2.5f);
                color.r *= shade; color.g *= shade; color.b *= shade; color.a = alpha;
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        static Texture2D MakePill()
        {
            const int size = 64;
            var texture = NewTexture(size, "Mochi procedural round");
            var pixels = new Color[size * size];
            const float radius = 12;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (31.5f - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (31.5f - radius), 0);
                pixels[y * size + x] = new Color(1, 1, 1,
                    Mathf.Clamp01((radius - Mathf.Sqrt(dx * dx + dy * dy)) * 1.5f));
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        static Texture2D MakeTriangle(Color top, Color bottom, bool inverted = false)
        {
            const int size = 128;
            var texture = NewTexture(size, "Mochi procedural ear");
            var pixels = new Color[size * size];
            Vector2 a = new Vector2(64, 6), b = new Vector2(8, 122), c = new Vector2(120, 122);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                // Texture y=0 is its bottom, while the artist's canvas y=0 is its top.
                Vector2 p = new Vector2(x + .5f, inverted ? y + .5f : size - y - .5f);
                float d1 = EdgeDistance(a, b, p), d2 = EdgeDistance(b, c, p), d3 = EdgeDistance(c, a, p);
                float alpha = Mathf.Clamp01(Mathf.Min(d1, Mathf.Min(d2, d3)) + .5f);
                // Softening the tip keeps the ears plush at small window sizes.
                float tip = Mathf.Clamp01((p.y - 5) * .28f);
                Color color = Color.Lerp(top, bottom, p.y / size);
                color.a = alpha * tip; pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        static float EdgeDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 edge = b - a;
            // Vertices are clockwise in screen coordinates, so the inside is on the right.
            return -(edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / edge.magnitude;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Release(oval); Release(pill); Release(triangle); Release(triangleDown); Release(silver);
            Release(silverBody); Release(cream); Release(earSilver);
            oval = pill = triangle = triangleDown = silver = silverBody = cream = earSilver = null;
        }

        static void Release(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
