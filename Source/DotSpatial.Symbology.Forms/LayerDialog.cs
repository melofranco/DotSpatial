// Copyright (c) DotSpatial Team. All rights reserved.
// Licensed under the MIT license. See License.txt file in the project root for full license information.

using System;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace DotSpatial.Symbology.Forms
{
    /// <summary>
    /// This is a basic form which is displayed when the user double-clicks on a layer name
    /// in the legend
    /// </summary>
    public partial class LayerDialog : Form
    {
        #region Fields

        private readonly ILayer _layer;
        private ICategoryControl _rasterCategoryControl;
        private CultureInfo _layerDialogCulture;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LayerDialog"/> class.
        /// </summary>
        public LayerDialog()
        {
            InitializeComponent();
            LayerDialogCulture = new CultureInfo(string.Empty);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LayerDialog"/> class to display the symbology and
        /// other properties of the specified feature layer
        /// </summary>
        /// <param name="selectedLayer">the specified feature layer that is
        /// modified using this form</param>
        /// <param name="control">The control.</param>
        public LayerDialog(ILayer selectedLayer, ICategoryControl control)
            : this()
        {
            _layer = selectedLayer;
            propertyGrid1.SelectedObject = _layer;
            LayerDialogCulture = _layer.LayerCulture;

            control.FeatCategControlCulture = _layerDialogCulture;
            Configure(control);
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the apply changes situation forces the symbology to become updated.
        /// </summary>
        public event EventHandler ChangesApplied;

        #endregion

        #region Porperties

        /// <summary>
        /// Gets or sets a value indicating the culture to use for resources.
        /// </summary>
        public CultureInfo LayerDialogCulture
        {
            get
            {
                return _layerDialogCulture;
            }

            set
            {
                if (_layerDialogCulture == value) return;

                _layerDialogCulture = value;
                if (_layerDialogCulture == null) _layerDialogCulture = new CultureInfo(string.Empty);

                Thread.CurrentThread.CurrentCulture = _layerDialogCulture;
                Thread.CurrentThread.CurrentUICulture = _layerDialogCulture;
                Refresh();

                UpdateLayerDialogResources();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Forces changes to be written from the copy symbology to
        /// the original, updating the map display.
        /// </summary>
        public void ApplyChanges()
        {
            try
            {
                OnApplyChanges();
            }
            catch (SyntaxErrorException)
            {
                MessageBox.Show(SymbologyFormsMessageStrings.LayerDialog_InvalidExpressionProvided);
            }
        }

        /// <summary>
        /// Occurs during apply changes operations and is overrideable in subclasses
        /// </summary>
        protected virtual void OnApplyChanges()
        {
            _rasterCategoryControl.ApplyChanges();

            ChangesApplied?.Invoke(_layer, EventArgs.Empty);
        }

        private void Configure(ICategoryControl control)
        {
            var userControl = control as UserControl;
            if (userControl != null)
            {
                userControl.Parent = pnlContent;
                userControl.Visible = true;
            }

            _rasterCategoryControl = control;
            _rasterCategoryControl.Initialize(_layer);
        }

        private void DialogButtons1ApplyClicked(object sender, EventArgs e)
        {
            OnApplyChanges();
        }

        private void DialogButtons1CancelClicked(object sender, EventArgs e)
        {
            _rasterCategoryControl.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void DialogButtons1OkClicked(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            OnApplyChanges();
            Close();
        }

        private void UpdateLayerDialogResources()
        {
            resources.ApplyResources(tabControl1, "tabControl1");
            resources.ApplyResources(tabSymbology, "tabSymbology");
            resources.ApplyResources(pnlContent, "pnlContent");
            resources.ApplyResources(tabDetails, "tabDetails");
            resources.ApplyResources(propertyGrid1, "propertyGrid1");
            resources.ApplyResources(panel1, "panel1");
            resources.ApplyResources(dialogButtons1, "dialogButtons1");
            resources.ApplyResources(this, "$this");

            if (_rasterCategoryControl != null) _rasterCategoryControl.FeatCategControlCulture = _layerDialogCulture;
            if (dialogButtons1 != null)
            {
                dialogButtons1.ButtonsCulture = _layerDialogCulture;
                dialogButtons1.Refresh();
            }
        }

        #endregion
    }
}