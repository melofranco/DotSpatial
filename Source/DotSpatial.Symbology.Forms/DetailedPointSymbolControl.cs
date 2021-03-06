// Copyright (c) DotSpatial Team. All rights reserved.
// Licensed under the MIT license. See License.txt file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using DotSpatial.Data;
using DotSpatial.Serialization;

namespace DotSpatial.Symbology.Forms
{
    /// <summary>
    /// DetailedPointSymbolDialog
    /// </summary>
    public partial class DetailedPointSymbolControl : UserControl
    {
        #region Fields

        private bool _disableUnitWarning;
        private bool _ignoreChanges;
        private IPointSymbolizer _original;
        private IPointSymbolizer _symbolizer;
        private CultureInfo _dPSControlCulture;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DetailedPointSymbolControl"/> class.
        /// </summary>
        public DetailedPointSymbolControl()
        {
            Configure();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DetailedPointSymbolControl"/> class that will update the specified original symbol
        /// by copying the new aspects to it only if the Apply Changes, or Ok buttons are used.
        /// </summary>
        /// <param name="original">The original IPointSymbolizer to modify.</param>
        public DetailedPointSymbolControl(IPointSymbolizer original)
        {
            Configure();
            Initialize(original);
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the add to custom symbols button is pressed.
        /// </summary>
        public event EventHandler<PointSymbolizerEventArgs> AddToCustomSymbols;

        /// <summary>
        /// Occurs whenever the apply changes button is clicked, or else when the ok button is clicked.
        /// </summary>
        public event EventHandler ChangesApplied;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the current (copied) symbolizer or initializes this control to work with the
        /// specified symbolizer as the original.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IPointSymbolizer Symbolizer
        {
            get
            {
                return _symbolizer;
            }

            set
            {
                if (value != null) Initialize(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the culture of the App.
        /// </summary>
        public CultureInfo DPSControlCulture
        {
            get
            {
                return _dPSControlCulture;
            }

            set
            {
                if (_dPSControlCulture == value) return;
                _dPSControlCulture = value;
                if (_dPSControlCulture == null) _dPSControlCulture = new CultureInfo(string.Empty);

                Thread.CurrentThread.CurrentCulture = _dPSControlCulture;
                Thread.CurrentThread.CurrentUICulture = _dPSControlCulture;

                UpdatePointSymbolResources();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the specified changes.
        /// </summary>
        public void ApplyChanges()
        {
            OnApplyChanges();
        }

        /// <summary>
        /// Resets the existing control with the new symbolizer.
        /// </summary>
        /// <param name="original">The original symbolizer.</param>
        public void Initialize(IPointSymbolizer original)
        {
            _original = original;
            _symbolizer = original.Copy();
            ccSymbols.Symbols = _symbolizer.Symbols;
            ccSymbols.RefreshList();
            if (_symbolizer.Symbols.Count > 0)
            {
                ccSymbols.SelectedSymbol = _symbolizer.Symbols[0];
            }

            UpdatePreview();
            UpdateSymbolControls();
        }

        /// <summary>
        /// Fires the AddtoCustomSymbols event.
        /// </summary>
        protected virtual void OnAddToCustomSymbols()
        {
            AddToCustomSymbols?.Invoke(this, new PointSymbolizerEventArgs(_symbolizer));
        }

        /// <summary>
        /// Fires the ChangesApplied event.
        /// </summary>
        protected virtual void OnApplyChanges()
        {
            // This duplicates the content from the edit copy, but leaves the original object reference intact so that the map updates.
            _original.CopyProperties(_symbolizer);
            ChangesApplied?.Invoke(this, EventArgs.Empty);
        }

        private void AngleControlSimpleAngleChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            ISymbol s = ccSymbols.SelectedSymbol;
            if (s != null)
            {
                s.Angle = -angleControl.Angle;
            }

            UpdatePreview();
        }

        private void BtnAddToCustomClick(object sender, EventArgs e)
        {
            OnAddToCustomSymbols();
        }

        private void BtnBrowseImageClick(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;

            using (OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = @"Image Files|*.bmp;*.jpg;*.gif;*.tif;*.png;*.ico"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                IPictureSymbol ps = ccSymbols.SelectedSymbol as IPictureSymbol;
                if (ps != null)
                {
                    ps.ImageFilename = ofd.FileName;
                    txtImageFilename.Text = Path.GetFileName(ofd.FileName);
                }

                UpdatePreview();
            }
        }

        private void CbColorCharacterColorChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IColorable c = ccSymbols.SelectedSymbol as IColorable;
            if (c != null)
            {
                c.Color = cbColorCharacter.Color;
                sldOpacityCharacter.MaximumColor = Color.FromArgb(255, c.Color);
                sldOpacityCharacter.Value = c.Opacity;
            }

            UpdatePreview();
        }

        private void CbColorSimpleColorChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IColorable c = ccSymbols.SelectedSymbol as IColorable;
            if (c != null)
            {
                c.Color = cbColorSimple.Color;
                sldOpacitySimple.MaximumColor = Color.FromArgb(255, c.Color);
                sldOpacitySimple.Value = c.Opacity;
                sldOpacitySimple.Invalidate();
            }

            UpdatePreview();
        }

        private void CbOutlineColorColorChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineColor = cbOutlineColor.Color;
                sldOutlineOpacity.MaximumColor = Color.FromArgb(255, cbOutlineColor.Color);
                sldOutlineOpacity.Value = os.OutlineOpacity;
                sldOutlineOpacity.Refresh();
            }

            UpdatePreview();
        }

        private void CbOutlineColorPictureColorChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineColor = cbOutlineColorPicture.Color;
                sldOutlineOpacityPicture.MaximumColor = Color.FromArgb(255, os.OutlineColor);
                sldOutlineOpacityPicture.Value = os.OutlineOpacity;
                sldOutlineOpacityPicture.Invalidate();
                UpdatePreview();
            }
        }

        private void CcSymbolsAddClicked(object sender, EventArgs e)
        {
            ISymbol s = null;
            string type = cmbSymbolType.SelectedItem.ToString();
            switch (type)
            {
                case "Simple":
                    s = new SimpleSymbol();
                    break;
                case "Character":
                    s = new CharacterSymbol();
                    break;
                case "Picture":
                    s = new PictureSymbol();
                    break;
            }

            if (s != null) ccSymbols.Symbols.Add(s);
            ccSymbols.RefreshList();
            UpdatePreview();
        }

        private void CcSymbolsListChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void CcSymbolsSelectedItemChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            if (ccSymbols.SelectedSymbol != null)
            {
                UpdateSymbolControls();
            }

            UpdatePreview();
        }

        private void CharCharacterPopupClicked(object sender, EventArgs e)
        {
            ICharacterSymbol cs = ccSymbols.SelectedSymbol as ICharacterSymbol;
            if (cs != null)
            {
                cs.Code = charCharacter.SelectedChar;
            }

            UpdatePreview();
        }

        private void ChkSmoothingCheckedChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            _symbolizer.Smoothing = chkSmoothing.Checked;
            UpdatePreview();
        }

        private void ChkUseOutlineCheckedChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.UseOutline = chkUseOutline.Checked;
            }

            UpdatePreview();
        }

        private void ChkUseOutlinePictureCheckedChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.UseOutline = chkUseOutlinePicture.Checked;
                UpdatePreview();
            }
        }

        private void CmbFontFamillySelectedItemChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            ICharacterSymbol cs = ccSymbols.SelectedSymbol as ICharacterSymbol;
            if (cs != null)
            {
                cs.FontFamilyName = cmbFontFamilly.SelectedFamily;
                charCharacter.Font = new Font(cs.FontFamilyName, 10, cs.Style);

                // charCharacter.
            }

            UpdatePreview();
        }

        private void CmbPointShapeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            ISimpleSymbol ss = ccSymbols.SelectedSymbol as ISimpleSymbol;
            if (ss == null) return;
            ss.PointShape = Global.ParseEnum<PointShape>(cmbPointShape.SelectedItem.ToString());
            UpdatePreview();
        }

        private void CmbScaleModeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            _symbolizer.ScaleMode = Global.ParseEnum<ScaleMode>(cmbScaleMode.SelectedItem.ToString());
        }

        private void CmbSymbolTypeSelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbSymbolType.SelectedItem.ToString() == "Simple")
            {
                if (tabSymbolProperties.TabPages.Contains(tabPicture))
                {
                    tabSymbolProperties.TabPages.Remove(tabPicture);
                }

                if (tabSymbolProperties.TabPages.Contains(tabCharacter))
                {
                    tabSymbolProperties.TabPages.Remove(tabCharacter);
                }

                if (tabSymbolProperties.TabPages.Contains(tabSimple) == false)
                {
                    tabSymbolProperties.TabPages.Insert(0, tabSimple);
                    tabSymbolProperties.SelectedTab = tabSimple;
                }
            }

            if (cmbSymbolType.SelectedItem.ToString() == "Character")
            {
                if (tabSymbolProperties.TabPages.Contains(tabPicture))
                {
                    tabSymbolProperties.TabPages.Remove(tabPicture);
                }

                if (tabSymbolProperties.TabPages.Contains(tabSimple))
                {
                    tabSymbolProperties.TabPages.Remove(tabSimple);
                }

                if (tabSymbolProperties.TabPages.Contains(tabCharacter) == false)
                {
                    tabSymbolProperties.TabPages.Insert(0, tabCharacter);
                    tabSymbolProperties.SelectedTab = tabCharacter;
                }
            }

            if (cmbSymbolType.SelectedItem.ToString() == "Picture")
            {
                if (tabSymbolProperties.TabPages.Contains(tabSimple))
                {
                    tabSymbolProperties.TabPages.Remove(tabSimple);
                }

                if (tabSymbolProperties.TabPages.Contains(tabCharacter))
                {
                    tabSymbolProperties.TabPages.Remove(tabCharacter);
                }

                if (tabSymbolProperties.TabPages.Contains(tabPicture) == false)
                {
                    tabSymbolProperties.TabPages.Insert(0, tabPicture);
                    tabSymbolProperties.SelectedTab = tabPicture;
                }
            }

            if (_ignoreChanges) return;

            int index = ccSymbols.Symbols.IndexOf(ccSymbols.SelectedSymbol);
            if (index == -1) return;
            ISymbol oldSymbol = ccSymbols.SelectedSymbol;

            if (cmbSymbolType.SelectedItem.ToString() == "Simple")
            {
                SimpleSymbol ss = new SimpleSymbol();
                if (oldSymbol != null) ss.CopyPlacement(oldSymbol);
                ccSymbols.Symbols[index] = ss;
                ccSymbols.RefreshList();
                ccSymbols.SelectedSymbol = ss;
            }

            if (cmbSymbolType.SelectedItem.ToString() == "Character")
            {
                CharacterSymbol cs = new CharacterSymbol();
                if (oldSymbol != null) cs.CopyPlacement(oldSymbol);
                ccSymbols.Symbols[index] = cs;
                ccSymbols.RefreshList();
                ccSymbols.SelectedSymbol = cs;
            }

            if (cmbSymbolType.SelectedItem.ToString() == "Picture")
            {
                PictureSymbol ps = new PictureSymbol();
                if (oldSymbol != null) ps.CopyPlacement(oldSymbol);
                ccSymbols.Symbols[index] = ps;
                ccSymbols.RefreshList();
                ccSymbols.SelectedSymbol = ps;
            }
        }

        private void CmbUnitsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_disableUnitWarning) return;
            if (cmbUnits.SelectedItem.ToString() == "World" && _symbolizer.ScaleMode != ScaleMode.Geographic)
            {
                if (MessageBox.Show(SymbologyFormsMessageStrings.ForceDrawingToUseGeographicScaleMode, SymbologyFormsMessageStrings.ChangeScaleMode, MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    cmbUnits.SelectedItem = _symbolizer.Units.ToString();
                    return;
                }

                _symbolizer.ScaleMode = ScaleMode.Geographic;
            }

            if (cmbUnits.SelectedItem.ToString() != "World" && _symbolizer.ScaleMode == ScaleMode.Geographic)
            {
                if (MessageBox.Show(SymbologyFormsMessageStrings.ForceDrawingToUseSymbolicScaleMode, SymbologyFormsMessageStrings.ChangeScaleMode, MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    cmbUnits.SelectedItem = _symbolizer.Units.ToString();
                    return;
                }

                _symbolizer.ScaleMode = ScaleMode.Symbolic;
            }

            GraphicsUnit destination = (GraphicsUnit)Enum.Parse(typeof(GraphicsUnit), cmbUnits.SelectedItem.ToString());

            GraphicsUnit source = _symbolizer.Units;
            double scale = 1;
            if (source == GraphicsUnit.Inch && destination == GraphicsUnit.Millimeter)
            {
                scale = 25.4;
            }

            if (source == GraphicsUnit.Millimeter && destination == GraphicsUnit.Inch)
            {
                scale = 1 / 25.4;
            }

            _symbolizer.Scale(scale);

            UpdateSymbolControls();
        }

        private void Configure()
        {
            InitializeComponent();
            DPSControlCulture = new CultureInfo(string.Empty);

            ccSymbols.AddClicked += CcSymbolsAddClicked;
            ccSymbols.SelectedItemChanged += CcSymbolsSelectedItemChanged;
            ccSymbols.ListChanged += CcSymbolsListChanged;
            lblPreview.Paint += LblPreviewPaint;
        }

        private void DbxOffsetXSimpleTextChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            ISymbol s = ccSymbols.SelectedSymbol;
            if (s != null)
            {
                s.Offset.X = dbxOffsetX.Value;
                UpdatePreview();
            }
        }

        private void DbxOffsetYSimpleTextChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            ISymbol s = ccSymbols.SelectedSymbol;
            if (s != null)
            {
                s.Offset.Y = dbxOffsetY.Value;
                UpdatePreview();
            }
        }

        private void DbxOutlineWidthTextChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineWidth = dbxOutlineWidth.Value;
            }

            UpdatePreview();
        }

        private void DbxOutlineWidthPictureTextChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineWidth = dbxOutlineWidthPicture.Value;
                UpdatePreview();
            }
        }

        private void LblPreviewPaint(object sender, PaintEventArgs e)
        {
            UpdatePreview(e.Graphics);
        }

        private void SizeControlSimpleSelectedSizeChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            UpdatePreview();
        }

        private void SldImageOpacityValueChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IPictureSymbol ps = ccSymbols.SelectedSymbol as IPictureSymbol;
            if (ps != null)
            {
                ps.Opacity = (float)sldImageOpacity.Value;
                UpdatePreview();
            }
        }

        private void SldOpacityCharacterValueChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IColorable c = ccSymbols.SelectedSymbol as IColorable;
            if (c != null)
            {
                c.Opacity = (float)sldOpacityCharacter.Value;
                cbColorCharacter.Color = c.Color;
            }

            UpdatePreview();
        }

        private void SldOpacitySimpleValueChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IColorable c = ccSymbols.SelectedSymbol as IColorable;
            if (c != null)
            {
                c.Opacity = (float)sldOpacitySimple.Value;
                cbColorSimple.Color = c.Color;
            }

            UpdatePreview();
        }

        private void SldOutlineOpacityValueChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineOpacity = (float)sldOutlineOpacity.Value;
                cbOutlineColor.Color = os.OutlineColor;
            }

            UpdatePreview();
        }

        private void SldOutlineOpacityPictureValueChanged(object sender, EventArgs e)
        {
            if (_ignoreChanges) return;
            IOutlinedSymbol os = ccSymbols.SelectedSymbol as IOutlinedSymbol;
            if (os != null)
            {
                os.OutlineOpacity = (float)sldOutlineOpacityPicture.Value;
                cbOutlineColorPicture.Color = os.OutlineColor;
                UpdatePreview();
            }
        }

        private void UpdateCharacterSymbolControls(ICharacterSymbol cs)
        {
            cmbFontFamilly.SelectedFamily = cs.FontFamilyName;
            txtUnicode.Text = ((int)cs.Character).ToString();
            charCharacter.TypeSet = (byte)(cs.Character / 256);
            charCharacter.SelectedChar = (byte)(cs.Character % 256);
            charCharacter.Font = new Font(cs.FontFamilyName, 10, cs.Style);
            cbColorCharacter.Color = cs.Color;
            sldOpacityCharacter.Value = (double)cs.Color.A / 255;
            sldOpacityCharacter.MaximumColor = Color.FromArgb(255, cs.Color);

            angleControl.Angle = -(int)cs.Angle;
            dbxOffsetX.Value = cs.Offset.X;
            dbxOffsetY.Value = cs.Offset.Y;
            sizeControl.Symbol = cs;
        }

        private void UpdatePictureSymbolControls(IPictureSymbol ps)
        {
            angleControl.Angle = -(int)ps.Angle;
            dbxOffsetX.Value = ps.Offset.X;
            dbxOffsetY.Value = ps.Offset.Y;
            sizeControl.Symbol = ps;

            sldImageOpacity.Value = ps.Opacity;
            txtImageFilename.Text = Path.GetFileName(ps.ImageFilename);

            chkUseOutlinePicture.Checked = ps.UseOutline;
            cbOutlineColorPicture.Color = ps.OutlineColor;
            sldOutlineOpacityPicture.Value = ps.OutlineOpacity;
            sldOutlineOpacityPicture.MaximumColor = Color.FromArgb(255, ps.OutlineColor);
            dbxOutlineWidth.Value = ps.OutlineWidth;
        }

        private void UpdatePreview(Graphics g)
        {
            if (_symbolizer == null) return;
            g.Clear(Color.White);
            Matrix shift = g.Transform;
            shift.Translate((float)lblPreview.Width / 2, (float)lblPreview.Height / 2);
            g.Transform = shift;
            double scale = 1;
            Size2D symbolSize = _symbolizer.GetSize();
            if (_symbolizer.ScaleMode == ScaleMode.Geographic || symbolSize.Height > (lblPreview.Height - 6))
            {
                scale = (lblPreview.Height - 6) / symbolSize.Height;
            }

            _symbolizer.Draw(g, scale);
        }

        private void UpdatePreview()
        {
            ccSymbols.Refresh();
            sizeControl.Refresh();
            Graphics g = lblPreview.CreateGraphics();
            UpdatePreview(g);
            g.Dispose();
        }

        private void UpdateSimpleSymbolControls(ISimpleSymbol ss)
        {
            angleControl.Angle = -(int)ss.Angle;
            dbxOffsetX.Value = ss.Offset.X;
            dbxOffsetY.Value = ss.Offset.Y;
            sizeControl.Symbol = ss;

            cmbPointShape.SelectedItem = ss.PointShape.ToString();
            cbColorSimple.Color = ss.Color;
            sldOpacitySimple.Value = ss.Opacity;
            sldOpacitySimple.MaximumColor = Color.FromArgb(255, ss.Color);
            chkUseOutline.Checked = ss.UseOutline;
            dbxOutlineWidth.Value = ss.OutlineWidth;
            cbOutlineColor.Color = ss.OutlineColor;
            sldOutlineOpacity.Value = ss.Opacity;
            sldOutlineOpacity.MaximumColor = Color.FromArgb(255, ss.OutlineColor);
        }

        private void UpdateSymbolControls()
        {
            _ignoreChanges = true;
            cmbScaleMode.SelectedItem = _symbolizer.ScaleMode.ToString();
            chkSmoothing.Checked = _symbolizer.Smoothing;
            _disableUnitWarning = true;
            cmbUnits.SelectedItem = _symbolizer.Units.ToString();
            _disableUnitWarning = false;
            ICharacterSymbol cs = ccSymbols.SelectedSymbol as ICharacterSymbol;
            if (cs != null)
            {
                cmbSymbolType.SelectedItem = "Character";
                UpdateCharacterSymbolControls(cs);
            }

            ISimpleSymbol ss = ccSymbols.SelectedSymbol as ISimpleSymbol;
            if (ss != null)
            {
                cmbSymbolType.SelectedItem = "Simple";
                UpdateSimpleSymbolControls(ss);
            }

            IPictureSymbol ps = ccSymbols.SelectedSymbol as IPictureSymbol;
            if (ps != null)
            {
                cmbSymbolType.SelectedItem = "Picture";
                UpdatePictureSymbolControls(ps);
            }

            _ignoreChanges = false;
        }

        private void UpdatePointSymbolResources()
        {
            resources.ApplyResources(btnAddToCustom, "btnAddToCustom");
            resources.ApplyResources(groupBox1, "groupBox1");
            resources.ApplyResources(cmbUnits, "cmbUnits");
            resources.ApplyResources(lblUnits, "lblUnits");
            resources.ApplyResources(lblScaleMode, "lblScaleMode");
            resources.ApplyResources(cmbScaleMode, "cmbScaleMode");
            resources.ApplyResources(chkSmoothing, "chkSmoothing");
            resources.ApplyResources(label3, "label3");
            resources.ApplyResources(lblPreview, "lblPreview");
            resources.ApplyResources(lblSymbolType, "lblSymbolType");
            resources.ApplyResources(cmbSymbolType, "cmbSymbolType");
            resources.ApplyResources(label10, "label10");
            resources.ApplyResources(groupBox6, "groupBox6");
            resources.ApplyResources(doubleBox7, "doubleBox7");
            resources.ApplyResources(doubleBox8, "doubleBox8");
            resources.ApplyResources(groupBox7, "groupBox7");
            resources.ApplyResources(doubleBox10, "doubleBox10");
            resources.ApplyResources(doubleBox11, "doubleBox11");
            resources.ApplyResources(tabPicture, "tabPicture");
            resources.ApplyResources(lblImageOpacity, "lblImageOpacity");
            resources.ApplyResources(grpOutlinePicture, "grpOutlinePicture");
            resources.ApplyResources(cbOutlineColorPicture, "cbOutlineColorPicture");
            resources.ApplyResources(sldOutlineOpacityPicture, "sldOutlineOpacityPicture");
            resources.ApplyResources(lblOutlineColorPicture, "lblOutlineColorPicture");
            resources.ApplyResources(dbxOutlineWidthPicture, "dbxOutlineWidthPicture");

            resources.ApplyResources(chkUseOutlinePicture, "chkUseOutlinePicture");
            resources.ApplyResources(btnBrowseImage, "btnBrowseImage");
            resources.ApplyResources(txtImageFilename, "txtImageFilename");
            resources.ApplyResources(lblImage, "lblImage");
            resources.ApplyResources(sldImageOpacity, "sldImageOpacity");
            resources.ApplyResources(tabCharacter, "tabCharacter");
            resources.ApplyResources(lblColorCharacter, "lblColorCharacter");
            resources.ApplyResources(txtUnicode, "txtUnicode");
            resources.ApplyResources(lblUnicode, "lblUnicode");
            resources.ApplyResources(lblFontFamily, "lblFontFamily");
            resources.ApplyResources(cbColorCharacter, "cbColorCharacter");
            resources.ApplyResources(sldOpacityCharacter, "sldOpacityCharacter");
            resources.ApplyResources(charCharacter, "charCharacter");
            resources.ApplyResources(cmbFontFamilly, "cmbFontFamilly");
            resources.ApplyResources(tabSimple, "tabSimple");
            resources.ApplyResources(groupBox2, "groupBox2");
            resources.ApplyResources(cbOutlineColor, "cbOutlineColor");
            resources.ApplyResources(sldOutlineOpacity, "sldOutlineOpacity");
            resources.ApplyResources(lblOutlineColor, "lblOutlineColor");
            resources.ApplyResources(dbxOutlineWidth, "dbxOutlineWidth");
            resources.ApplyResources(chkUseOutline, "chkUseOutline");
            resources.ApplyResources(cmbPointShape, "cmbPointShape");
            resources.ApplyResources(label2, "label2");
            resources.ApplyResources(lblColorSimple, "lblColorSimple");

            resources.ApplyResources(cbColorSimple, "cbColorSimple");
            resources.ApplyResources(sldOpacitySimple, "sldOpacitySimple");
            resources.ApplyResources(grpPlacement, "grpPlacement");
            resources.ApplyResources(grpOffset, "grpOffset");
            resources.ApplyResources(dbxOffsetX, "dbxOffsetX");
            resources.ApplyResources(dbxOffsetY, "dbxOffsetY");
            resources.ApplyResources(angleControl, "angleControl");
            resources.ApplyResources(sizeControl, "sizeControl");
            resources.ApplyResources(tabSymbolProperties, "tabSymbolProperties");
            resources.ApplyResources(ccSymbols, "ccSymbols");
            resources.ApplyResources(doubleBox9, "doubleBox9");
            resources.ApplyResources(this, "$this");
        }

        #endregion
    }
}