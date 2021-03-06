// Copyright (c) DotSpatial Team. All rights reserved.
// Licensed under the MIT license. See License.txt file in the project root for full license information.

using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using DotSpatial.Serialization;

namespace DotSpatial.Symbology.Forms
{
    /// <summary>
    /// Label Setup form.
    /// </summary>
    public partial class LabelSetup : Form
    {
        #region Fields

        private ILabelCategory _activeCategory = new LabelCategory(); // Set fake category to avoid null checks
        private bool _ignoreUpdates;
        private bool _isLineLayer;
        private ILabelLayer _layer;
        private ILabelLayer _original;
        private CultureInfo _labelSetupCulture;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSetup"/> class.
        /// </summary>
        public LabelSetup()
        {
            InitializeComponent();
            LabelSetupCulture = new CultureInfo(string.Empty);

            UpdateFontStyles();

            // Populate the label method drop downs
            foreach (var partMethod in Enum.GetValues(typeof(PartLabelingMethod))) cmbLabelParts.Items.Add(partMethod);
            foreach (var lo in Enum.GetValues(typeof(LineOrientation))) cmbLineAngle.Items.Add(lo);

            tabs.Selecting += TabsSelecting;
            TabsSelecting(tabs, new TabControlCancelEventArgs(tabs.SelectedTab, tabs.SelectedIndex, false, TabControlAction.Selecting));

            UpdatePreview();
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs after the Apply button has been pressed.
        /// </summary>
        public event EventHandler ChangesApplied;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the layer to use for defining this dialog.
        /// </summary>
        public ILabelLayer Layer
        {
            get
            {
                return _layer;
            }

            set
            {
                _original = value;
                if (_original != null) _layer = value.Copy();

                LabelSetupCulture = _layer.FeatureLayer.LayerCulture;
                UpdateLayer();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the culture to use for resources.
        /// </summary>
        public CultureInfo LabelSetupCulture
        {
            get
            {
                return _labelSetupCulture;
            }

            set
            {
                _labelSetupCulture = value;
                if (_labelSetupCulture == null) _labelSetupCulture = new CultureInfo(string.Empty);

                Thread.CurrentThread.CurrentCulture = _labelSetupCulture;
                Thread.CurrentThread.CurrentUICulture = _labelSetupCulture;
                Refresh();

                UpdateLabelSetupResources();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// When the layer is updated or configured, this updates the data Table aspects if possible.
        /// </summary>
        public void UpdateLayer()
        {
            UpdateCategories();
            if (lbCategories.Items.Count > 0) lbCategories.SelectedIndex = 0;
            cmbPriorityField.Items.Clear();
            cmbPriorityField.Items.Add("FID");
            cmbLabelAngleField.Items.Clear();
            if (_layer?.FeatureLayer?.DataSet?.DataTable == null) return;

            sqlExpression.Table = _layer.FeatureLayer.DataSet.DataTable;
            sqlMembers.Table = _layer.FeatureLayer.DataSet.DataTable;
            foreach (DataColumn column in _layer.FeatureLayer.DataSet.DataTable.Columns)
            {
                cmbPriorityField.Items.Add(column.ColumnName);
                cmbLabelAngleField.Items.Add(column.ColumnName);
            }

            _isLineLayer = _layer.FeatureLayer.Symbolizer is ILineSymbolizer;
            cmbLineAngle.Visible = _isLineLayer;
            rbLineBasedAngle.Visible = _isLineLayer;

            cmbLabelingMethod.Items.Clear();
            /*if (_isLineLayer)
            {
                foreach (var method in Enum.GetValues(typeof(LineLabelPlacementMethod))) cmbLabelingMethod.Items.Add(method);
            }
            else
            {
                foreach (var method in Enum.GetValues(typeof(LabelPlacementMethod))) cmbLabelingMethod.Items.Add(method);
            }*/
            if (_isLineLayer)
            {
                cmbLabelingMethod.Items.AddRange(new object[]
                {
                resources.GetString("cmbLabelingLineMethod.Items"),
                resources.GetString("cmbLabelingLineMethod.Items1"),
                resources.GetString("cmbLabelingLineMethod.Items2"),
                resources.GetString("cmbLabelingLineMethod.Items3")
                });
            }
            else
            {
                cmbLabelingMethod.Items.AddRange(new object[]
                {
                resources.GetString("cmbLabelingPolygMethod.Items"),
                resources.GetString("cmbLabelingPolygMethod.Items1"),
                resources.GetString("cmbLabelingPolygMethod.Items2")
                });
            }

            UpdateControls();
        }

        /// <summary>
        /// Updates any content that visually displays the currently selected characteristics.
        /// </summary>
        public void UpdatePreview()
        {
            if (cmbSize.SelectedItem == null) return;

            var size = float.Parse(cmbSize.SelectedItem.ToString());
            var style = (FontStyle)cmbStyle.SelectedIndex;

            try
            {
                lblPreview.Font = new Font(ffcFamilyName.SelectedFamily, size, style);
                lblPreview.BackColor = chkBackgroundColor.Checked ? cbBackgroundColor.Color : SystemColors.Control;
                lblPreview.ForeColor = cbFontColor.Color;
                lblPreview.Text = SymbologyFormsMessageStrings.Preview;
                lblPreview.Invalidate();
                ttLabelSetup.SetToolTip(lblPreview, SymbologyFormsMessageStrings.LabelSetup_ThisShowsAPreviewOfTheFont);
                _activeCategory.Symbolizer.FontFamily = ffcFamilyName.SelectedFamily;
                _activeCategory.Symbolizer.FontSize = size;
                _activeCategory.Symbolizer.FontStyle = style;
                _activeCategory.Symbolizer.FontColor = cbFontColor.Color;
            }
            catch
            {
                lblPreview.Font = new Font("Arial", 20F, FontStyle.Bold);
                lblPreview.Text = SymbologyFormsMessageStrings.Unsupported;
                ttLabelSetup.SetToolTip(lblPreview, SymbologyFormsMessageStrings.LabelSetup_TheSpecifiedCombinationOfFontFamilyStyleOrSizeIsUnsupported);
            }
        }

        /// <summary>
        /// Fires the ChangesApplied event.
        /// </summary>
        protected virtual void OnChangesApplied()
        {
            _activeCategory.Expression = sqlExpression.ExpressionText;

            ChangesApplied?.Invoke(this, EventArgs.Empty);

            if (_original != null)
            {
                _original.CopyProperties(_layer);
                _original.CreateLabels();

                // We have no guarantee that the MapFrame property is set, but redrawing the map is important.
                _original.FeatureLayer.MapFrame?.Invalidate();
            }
        }

        /// <summary>
        /// Adds a new category.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void BtnAddClick(object sender, EventArgs e)
        {
            lbCategories.Items.Insert(0, _layer.Symbology.AddCategory());
            sqlExpression.AllowEmptyExpression = lbCategories.Items.Count == 1;
        }

        /// <summary>
        /// Moves the selected category down.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void BtnCategoryDownClick(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            _layer.Symbology.Demote(cat);
            UpdateCategories();
            lbCategories.SelectedItem = cat;
        }

        /// <summary>
        /// Moves the selected category up.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void BtnCategoryUpClick(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            _layer.Symbology.Promote(cat);
            UpdateCategories();
            lbCategories.SelectedItem = cat;
        }

        /// <summary>
        /// Removes the selected category.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void BtnSubtractClick(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            if (cat == null) return;

            if (lbCategories.Items.Count == 1)
            {
                MessageBox.Show(this, SymbologyFormsMessageStrings.LabelSetup_OneCategoryNeededErr, SymbologyFormsMessageStrings.LabelSetup_OneCategoryNeededErrCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _ignoreUpdates = true;
            lbCategories.Items.Remove(cat);
            _layer.Symbology.Categories.Remove(cat);
            if (lbCategories.Items.Count > 0) lbCategories.SelectedIndex = 0;
            _ignoreUpdates = false;

            UpdateControls();
        }

        /// <summary>
        /// Remembers the BackgroundColor and updates the preview.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CbBackgroundColorColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BackColor = cbBackgroundColor.Color;
            if (!_ignoreUpdates) chkBackgroundColor.Checked = true;
            UpdatePreview();
        }

        /// <summary>
        /// Remembers the BorderColor and updates the preview.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CbBorderColorColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BorderColor = cbBorderColor.Color;
            if (!_ignoreUpdates)
            {
                _activeCategory.Symbolizer.BorderVisible = true;
                chkBorder.Checked = true;
            }

            UpdatePreview();
        }

        /// <summary>
        /// Rembemer selected FontColor and update Preview.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CbFontColorColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.FontColor = cbFontColor.Color;
            UpdatePreview();
        }

        /// <summary>
        /// Remembers whether BackColor is enabled.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkBackgroundColorCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BackColorEnabled = chkBackgroundColor.Checked;
            UpdateControls();
        }

        /// <summary>
        /// Remembers whether border gets used.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkBorderCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BorderVisible = chkBorder.Checked;
        }

        /// <summary>
        /// Remembers whether halo is used.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkHaloCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.HaloEnabled = chkHalo.Checked;
            clrHalo.Enabled = chkHalo.Checked;
        }

        /// <summary>
        /// Remember selected PreventCollision.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkPreventCollisionCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PreventCollisions = chkPreventCollision.Checked;
        }

        /// <summary>
        /// Remember whether low values get prioritized.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkPrioritizeLowCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PrioritizeLowValues = chkPrioritizeLow.Checked;
        }

        /// <summary>
        /// Remembers whether shadow is used.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ChkShadowCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowEnabled = chkShadow.Checked;
            gpbUseLabelShadow.Enabled = chkShadow.Checked;
        }

        /// <summary>
        /// Remembers the halo color.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ClrHaloSelectedItemChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.HaloColor = clrHalo.Value;
        }

        /// <summary>
        /// Remembers the alignment of multiline text.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbAlignmentSelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Alignment = (StringAlignment)cmbAlignment.SelectedIndex;
        }

        /// <summary>
        /// Remember the selected LabelAngleField.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbLabelAngleFieldSelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.LabelAngleField = (string)cmbLabelAngleField.SelectedItem;
        }

        /// <summary>
        /// Remembers the labeling method.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbLabelingMethodSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLineLayer)
            {
                // _activeCategory.Symbolizer.LineLabelPlacementMethod = (LineLabelPlacementMethod)cmbLabelingMethod.SelectedItem;
                _activeCategory.Symbolizer.LineLabelPlacementMethod = LineLabelPlacementMethod_FromIndex(cmbLabelingMethod.SelectedIndex);
            }
            else
            {
                // _activeCategory.Symbolizer.LabelPlacementMethod = (LabelPlacementMethod)cmbLabelingMethod.SelectedItem;
                _activeCategory.Symbolizer.LabelPlacementMethod = LabelPlacementMethod_FromIndex(cmbLabelingMethod.SelectedIndex);
            }
        }

        /// <summary>
        /// Remembers the way parts get labeled.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbLabelPartsSelectedIndexChanged(object sender, EventArgs e)
        {
            // _activeCategory.Symbolizer.PartsLabelingMethod = (PartLabelingMethod)cmbLabelParts.SelectedItem;
            _activeCategory.Symbolizer.PartsLabelingMethod = PartLabelingMethod_FromIndex(cmbLabelParts.SelectedIndex);
        }

        /// <summary>
        /// Remember selected LineOrientation.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbLineAngleSelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.LineOrientation = (LineOrientation)cmbLineAngle.SelectedItem;
        }

        /// <summary>
        /// Remember selected PriorityField.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbPriorityFieldSelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PriorityField = (string)cmbPriorityField.SelectedItem;
        }

        /// <summary>
        /// Shows the preview with the selected font size.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbSizeSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Shows the preview with the selected font style.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void CmbStyleSelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void CmdApplyClick(object sender, EventArgs e)
        {
            if (!sqlExpression.ValidateExpression())
            {
                tabs.SelectedTab = tabExpression;
                return;
            }

            if (!sqlMembers.ValidateExpression())
            {
                tabs.SelectedTab = tabMembers;
                return;
            }

            OnChangesApplied();
        }

        private void CmdCancelClick(object sender, EventArgs e)
        {
            Close();
        }

        private void CmdOkClick(object sender, EventArgs e)
        {
            if (!sqlExpression.ValidateExpression())
            {
                tabs.SelectedTab = tabExpression;
                DialogResult = DialogResult.None;
                return;
            }

            if (!sqlMembers.ValidateExpression())
            {
                tabs.SelectedTab = tabMembers;
                DialogResult = DialogResult.None;
                return;
            }

            OnChangesApplied();
            Close();
        }

        /// <summary>
        /// Remembers the shadows color.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void ColorButtonShadowColorChanged(object sender, EventArgs e)
        {
            if (colorButtonShadow.Color != colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value))
            {
                colorButtonShadow.Color = colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value);
                _activeCategory.Symbolizer.DropShadowColor = colorButtonShadow.Color;
            }
        }

        /// <summary>
        /// Shows the preview with the selected font and corrects the style-selection.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void FontFamilyControl1SelectedItemChanged(object sender, EventArgs e)
        {
            UpdateFontStyles();
            UpdatePreview();
        }

        /// <summary>
        /// Remembers the position of the label relative to the placement point.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void LabelAlignmentControl1ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Orientation = labelAlignmentControl1.Value;
        }

        /// <summary>
        /// Updates the controls with the data of the selected category.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void LbCategoriesSelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory = (ILabelCategory)lbCategories.SelectedItem ?? new LabelCategory(); // Set fake category to avoid null checks
            UpdateControls();
        }

        /// <summary>
        /// Save the changed angle.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void NudAngleValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Angle = (double)nudAngle.Value;
        }

        /// <summary>
        /// Remember the X offset of the label from the center of the placement point.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void NudXOffsetValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.OffsetX = (float)nudXOffset.Value;
        }

        /// <summary>
        /// Remember the Y offset of the label from the center of the placement point.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void NudYOffsetValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.OffsetY = (float)nudYOffset.Value;
        }

        /// <summary>
        /// De-/active NumericUpDown for common angle.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void RbCommonAngleCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseAngle = rbCommonAngle.Checked;
            nudAngle.Enabled = rbCommonAngle.Checked;
        }

        /// <summary>
        /// De-/activate combobox for LabelAngleField.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void RbIndividualAngleCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseLabelAngleField = rbIndividualAngle.Checked;
            cmbLabelAngleField.Enabled = rbIndividualAngle.Checked;
        }

        /// <summary>
        /// De-/activate combobox for linebased angles.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void RbLineBasedAngleCheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseLineOrientation = rbLineBasedAngle.Checked;
            cmbLineAngle.Enabled = rbLineBasedAngle.Checked;
        }

        /// <summary>
        /// Remembers the shadows opacity.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void SliderOpacityShadowValueChanged(object sender, EventArgs e)
        {
            colorButtonShadow.Color = colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value);
            _activeCategory.Symbolizer.DropShadowColor = colorButtonShadow.Color;
        }

        /// <summary>
        /// Remembers the expression that is used to find the members that belong to the active category.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void SqlMembersExpressionTextChanged(object sender, EventArgs e)
        {
            _activeCategory.FilterExpression = sqlMembers.ExpressionText;
        }

        /// <summary>
        /// Shows the help for the selected tab.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void TabsSelecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabMembers)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help2;
            }
            else if (e.TabPage == tabExpression)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help3;
            }
            else if (e.TabPage == tabBasic)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help4;
            }
            else if (e.TabPage == tabAdvanced)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help5;
            }
            else
            {
                lblHelp.Visible = false;
            }
        }

        /// <summary>
        /// Remember floatingFormat.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void TbFloatingFormatTextChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.FloatingFormat = tbFloatingFormat.Text;
        }

        /// <summary>
        /// Updates the category list with the categories of the layers symbology.
        /// </summary>
        private void UpdateCategories()
        {
            lbCategories.SuspendLayout();
            lbCategories.Items.Clear();
            foreach (var cat in _layer.Symbology.Categories)
            {
                lbCategories.Items.Insert(0, cat);
            }

            lbCategories.ResumeLayout();
        }

        /// <summary>
        /// Updates the controls with the data of the active categories symbolizer.
        /// </summary>
        private void UpdateControls()
        {
            if (_ignoreUpdates) return;

            _ignoreUpdates = true;

            var symb = _activeCategory.Symbolizer;

            cmbPriorityField.SelectedItem = symb.PriorityField;
            chkPreventCollision.Checked = symb.PreventCollisions;
            chkPrioritizeLow.Checked = symb.PrioritizeLowValues;

            // Font color & opacity. Set opacity first.
            sldFontOpacity.Value = symb.FontColor.GetOpacity();
            cbFontColor.Color = symb.FontColor;

            // Background color & opacity. Set opacity first.
            sldBackgroundOpacity.Value = symb.BackColor.GetOpacity();
            cbBackgroundColor.Color = symb.BackColor;
            chkBackgroundColor.Checked = symb.BackColorEnabled;

            // Border color & opacity. Set opacity first.
            chkBorder.Checked = symb.BorderVisible;
            sldBorderOpacity.Value = symb.BorderColor.GetOpacity();
            cbBorderColor.Color = symb.BorderColor;

            cmbSize.SelectedItem = symb.FontSize.ToString(CultureInfo.InvariantCulture);
            cmbAlignment.SelectedIndex = (int)symb.Alignment;
            ffcFamilyName.SelectedFamily = symb.FontFamily;
            cmbStyle.SelectedIndex = (int)symb.FontStyle;
            sqlExpression.ExpressionText = _activeCategory.Expression;
            sqlExpression.AllowEmptyExpression = lbCategories.Items.Count == 1;
            sqlMembers.ExpressionText = _activeCategory.FilterExpression;
            labelAlignmentControl1.Value = symb.Orientation;

            // Shadow options.
            chkShadow.Checked = symb.DropShadowEnabled;
            sliderOpacityShadow.Value = symb.DropShadowColor.GetOpacity();
            colorButtonShadow.Color = symb.DropShadowColor;
            nudShadowOffsetX.Value = (decimal)symb.DropShadowPixelOffset.X;
            nudShadowOffsetY.Value = (decimal)symb.DropShadowPixelOffset.Y;

            nudYOffset.Value = (decimal)symb.OffsetY;
            nudXOffset.Value = (decimal)symb.OffsetX;
            clrHalo.Value = symb.HaloColor;
            chkHalo.Checked = symb.HaloEnabled;

            /*if (_isLineLayer) cmbLabelingMethod.SelectedItem = symb.LineLabelPlacementMethod;
            else cmbLabelingMethod.SelectedItem = symb.LabelPlacementMethod;*/
            Console.WriteLine("Update Controls  " + cmbLabelingMethod.Items.Count + "  " + symb.LabelPlacementMethod.ToString());
            if (cmbLabelingMethod.Items.Count > 0)
            {
                Console.WriteLine("Line   ...  " + symb.LineLabelPlacementMethod.ToString() + " --> " + LineLabelPlacementMethod_ToIndex(symb.LineLabelPlacementMethod));
                if (_isLineLayer) cmbLabelingMethod.SelectedIndex = 0; // LineLabelPlacementMethod_ToIndex(symb.LineLabelPlacementMethod);
                else cmbLabelingMethod.SelectedIndex = LabelPlacementMethod_ToIndex(symb.LabelPlacementMethod);
            }

            /*cmbLabelParts.SelectedItem = symb.PartsLabelingMethod;*/
            cmbLabelParts.SelectedIndex = PartLabelingMethod_ToIndex(symb.PartsLabelingMethod);

            // Label Rotation
            rbCommonAngle.Checked = symb.UseAngle;
            RbCommonAngleCheckedChanged(rbCommonAngle, EventArgs.Empty);
            nudAngle.Value = (decimal)symb.Angle;
            rbIndividualAngle.Checked = symb.UseLabelAngleField;
            RbIndividualAngleCheckedChanged(rbIndividualAngle, EventArgs.Empty);
            cmbLabelAngleField.SelectedItem = symb.LabelAngleField;
            rbLineBasedAngle.Checked = symb.UseLineOrientation;
            RbLineBasedAngleCheckedChanged(rbLineBasedAngle, EventArgs.Empty);
            cmbLineAngle.SelectedItem = symb.LineOrientation;

            // Floating format
            tbFloatingFormat.Text = symb.FloatingFormat;

            //--
            _ignoreUpdates = false;
        }

        /// <summary>
        /// Updates the FontStyles with the styles that exist for the selected font.
        /// </summary>
        private void UpdateFontStyles()
        {
            var ff = ffcFamilyName.GetSelectedFamily();
            cmbStyle.Items.Clear();
            for (var i = 0; i < 15; i++)
            {
                var fs = (FontStyle)i;
                if (ff.IsStyleAvailable(fs))
                {
                    cmbStyle.Items.Add(fs);
                }
            }

            cmbStyle.SelectedItem = cmbStyle.Items.Contains(FontStyle.Regular) ? FontStyle.Regular : cmbStyle.Items[0];
        }

        /// <summary>
        /// Remember the X offset of the labelshadow from the center of the placement point.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void UpDownShadowOffsetXValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowPixelOffset = new PointF((float)nudShadowOffsetX.Value, (float)nudShadowOffsetY.Value);
        }

        /// <summary>
        /// Remember the Y offset of the labelshadow from the center of the placement point.
        /// </summary>
        /// <param name="sender">Sender that raised the event.</param>
        /// <param name="e">The event args.</param>
        private void UpDownShadowOffsetYValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowPixelOffset = new PointF((float)nudShadowOffsetX.Value, (float)nudShadowOffsetY.Value);
        }

        private LineLabelPlacementMethod LineLabelPlacementMethod_FromIndex(int nIndex)
        {
            switch (nIndex)
            {
                case 0:
                    return LineLabelPlacementMethod.LongestSegment;

                case 1:
                    return LineLabelPlacementMethod.FirstSegment;

                case 2:
                    return LineLabelPlacementMethod.MiddleSegment;

                case 3:
                    return LineLabelPlacementMethod.LastSegment;

                default:
                    return LineLabelPlacementMethod.MiddleSegment;
            }
        }

        private LabelPlacementMethod LabelPlacementMethod_FromIndex(int nIndex)
        {
            switch (nIndex)
            {
                case 0:
                    return LabelPlacementMethod.Centroid;

                case 1:
                    return LabelPlacementMethod.Center;

                case 2:
                    return LabelPlacementMethod.InteriorPoint;

                default:
                    return LabelPlacementMethod.InteriorPoint;
            }
        }

        private PartLabelingMethod PartLabelingMethod_FromIndex(int nIndex)
        {
            switch (nIndex)
            {
                case 0:
                    return PartLabelingMethod.LabelAllParts;

                case 1:
                    return PartLabelingMethod.LabelLargestPart;

                default:
                    return PartLabelingMethod.LabelLargestPart;
            }
        }

        private int LineLabelPlacementMethod_ToIndex(LineLabelPlacementMethod linePlacement)
        {
            switch (linePlacement)
            {
                case LineLabelPlacementMethod.LongestSegment:
                    return 0;

                case LineLabelPlacementMethod.FirstSegment:
                    return 1;

                case LineLabelPlacementMethod.MiddleSegment:
                    return 2;

                case LineLabelPlacementMethod.LastSegment:
                    return 3;

                default:
                    return 2;
            }
        }

        private int LabelPlacementMethod_ToIndex(LabelPlacementMethod labelPlacement)
        {
            switch (labelPlacement)
            {
                case LabelPlacementMethod.Centroid:
                    return 0;

                case LabelPlacementMethod.Center:
                    return 1;

                case LabelPlacementMethod.InteriorPoint:
                    return 2;

                default:
                    return 2;
            }
        }

        private int PartLabelingMethod_ToIndex(PartLabelingMethod partLabeling)
        {
            switch (partLabeling)
            {
                case PartLabelingMethod.LabelAllParts:
                    return 0;

                case PartLabelingMethod.LabelLargestPart:
                    return 1;

                default:
                    return 1;
            }
        }

        private void UpdateLabelSetupResources()
        {
            resources.ApplyResources(splitContainer1, "splitContainer1");
            resources.ApplyResources(lbCategories, "lbCategories");
            resources.ApplyResources(panel3, "panel3");
            resources.ApplyResources(btnCategoryDown, "btnCategoryDown");
            resources.ApplyResources(btnCategoryUp, "btnCategoryUp");
            resources.ApplyResources(btnSubtract, "btnSubtract");
            resources.ApplyResources(btnAdd, "btnAdd");
            resources.ApplyResources(panel2, "panel2");
            resources.ApplyResources(lblSymbolGroups, "lblSymbolGroups");
            resources.ApplyResources(tabs, "tabs");
            resources.ApplyResources(tabExpression, "tabExpression");
            resources.ApplyResources(sqlExpression, "sqlExpression");
            sqlExpression.UpdateExpressionControlResources();

            resources.ApplyResources(tabBasic, "tabBasic");
            resources.ApplyResources(tbFloatingFormat, "tbFloatingFormat");
            resources.ApplyResources(label13, "label13");
            resources.ApplyResources(grbLabelRotation, "grbLabelRotation");
            resources.ApplyResources(rbLineBasedAngle, "rbLineBasedAngle");
            resources.ApplyResources(cmbLineAngle, "cmbLineAngle");
            resources.ApplyResources(cmbLabelAngleField, "cmbLabelAngleField");
            resources.ApplyResources(rbIndividualAngle, "rbIndividualAngle");
            resources.ApplyResources(rbCommonAngle, "rbCommonAngle");
            resources.ApplyResources(nudAngle, "nudAngle");
            resources.ApplyResources(chkPrioritizeLow, "chkPrioritizeLow");
            resources.ApplyResources(chkPreventCollision, "chkPreventCollision");
            resources.ApplyResources(lblPriorityField, "lblPriorityField");
            resources.ApplyResources(cmbPriorityField, "cmbPriorityField");
            resources.ApplyResources(gpbBorderColor, "gpbBorderColor");
            resources.ApplyResources(chkBorder, "chkBorder");
            resources.ApplyResources(sldBorderOpacity, "sldBorderOpacity");
            resources.ApplyResources(cbBorderColor, "cbBorderColor");
            resources.ApplyResources(gpbFont, "gpbFont");
            resources.ApplyResources(lblFontColor, "lblFontColor");
            resources.ApplyResources(sldFontOpacity, "sldFontOpacity");
            resources.ApplyResources(cbFontColor, "cbFontColor");
            resources.ApplyResources(cmbStyle, "cmbStyle");
            resources.ApplyResources(lblFamily, "lblFamily");
            resources.ApplyResources(label2, "label2");

            resources.ApplyResources(label1, "label1");
            resources.ApplyResources(ffcFamilyName, "ffcFamilyName");
            resources.ApplyResources(lblPreview, "lblPreview");
            resources.ApplyResources(gpbBackgroundColor, "gpbBackgroundColor");
            resources.ApplyResources(sldBackgroundOpacity, "sldBackgroundOpacity");
            resources.ApplyResources(cbBackgroundColor, "cbBackgroundColor");
            resources.ApplyResources(chkBackgroundColor, "chkBackgroundColor");
            resources.ApplyResources(tabAdvanced, "tabAdvanced");
            resources.ApplyResources(labelAlignmentControl1, "labelAlignmentControl1");
            resources.ApplyResources(grpOffset, "grpOffset");
            resources.ApplyResources(nudYOffset, "nudYOffset");
            resources.ApplyResources(label11, "label11");
            resources.ApplyResources(label12, "label12");
            resources.ApplyResources(nudXOffset, "nudXOffset");
            resources.ApplyResources(label10, "label10");
            resources.ApplyResources(chkHalo, "chkHalo");
            resources.ApplyResources(chkShadow, "chkShadow");
            resources.ApplyResources(label5, "label5");
            resources.ApplyResources(label4, "label4");
            resources.ApplyResources(label3, "label3");

            resources.ApplyResources(cmbAlignment, "cmbAlignment");
            cmbAlignment.Items.Clear();
            cmbAlignment.Items.AddRange(new object[]
            {
            resources.GetString("cmbAlignment.Items"),
            resources.GetString("cmbAlignment.Items1"),
            resources.GetString("cmbAlignment.Items2")
            });

            resources.ApplyResources(cmbLabelingMethod, "cmbLabelingMethod");

            resources.ApplyResources(cmbLabelParts, "cmbLabelParts");
            cmbLabelParts.Items.Clear();
            cmbLabelParts.Items.AddRange(new object[]
            {
            resources.GetString("cmbLabelParts.Items"),
            resources.GetString("cmbLabelParts.Items1")
            });

            resources.ApplyResources(groupBox1, "groupBox1");
            resources.ApplyResources(clrHalo, "clrHalo");
            resources.ApplyResources(gpbUseLabelShadow, "gpbUseLabelShadow");
            resources.ApplyResources(nudShadowOffsetY, "nudShadowOffsetY");
            resources.ApplyResources(label9, "label9");
            resources.ApplyResources(label8, "label8");
            resources.ApplyResources(label7, "label7");
            resources.ApplyResources(nudShadowOffsetX, "nudShadowOffsetX");
            resources.ApplyResources(sliderOpacityShadow, "sliderOpacityShadow");
            resources.ApplyResources(label6, "label6");
            resources.ApplyResources(colorButtonShadow, "colorButtonShadow");
            resources.ApplyResources(tabMembers, "tabMembers");
            resources.ApplyResources(sqlMembers, "sqlMembers");
            resources.ApplyResources(cmdOK, "cmdOK");
            resources.ApplyResources(cmdCancel, "cmdCancel");
            resources.ApplyResources(cmdApply, "cmdApply");
            resources.ApplyResources(lblHelp, "lblHelp");
            resources.ApplyResources(panel1, "panel1");
            resources.ApplyResources(this, "$this");
        }

        #endregion
    }
}