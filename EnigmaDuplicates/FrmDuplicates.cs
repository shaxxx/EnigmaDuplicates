using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Windows.Forms;
using Krkadoni.EnigmaSettings;
using Krkadoni.EnigmaSettings.Interfaces;
using Krkadoni.EnigmaDuplicates.Properties;
using Settings = Krkadoni.EnigmaSettings.Settings;

namespace Krkadoni.EnigmaDuplicates
{
    public partial class FrmDuplicates : Form
    {

        private static FrmDuplicates _defInstance;
        public static FrmDuplicates DefInstance
        {
            get { return _defInstance ?? (_defInstance = new FrmDuplicates()); }
            set { _defInstance = value; }
        }

        public FrmDuplicates()
        {
            InitializeComponent();
            SetProgressBarVisible(false);
            UpdateInfoText();
            toolStripVersion.Text = @"ver. " + Application.ProductVersion;
        }

        private bool _isLoadingList;
        private bool _isSearchingDuplicates;
        private int _totalDuplicates;
        private Settings _settings;

        #region "Helper Methods"

        private void UpdateControlsEnabled()
        {
            InvokeIfRequired(toolStrip1, () =>
            {
                treeView.Enabled = !(_isLoadingList || _isSearchingDuplicates);
                toolStripLoad.Enabled = !(_isLoadingList || _isSearchingDuplicates);
                toolStripSave.Enabled = !(_isLoadingList || _isSearchingDuplicates) && _settings != null;
                openToolStripMenuItem.Enabled = toolStripLoad.Enabled;
                saveToolStripMenuItem.Enabled = toolStripSave.Enabled;
                allDuplicatesToolStripMenuItem.Enabled = _settings != null;
                duplicatesNotInBouquetsToolStripMenuItem.Enabled = _settings != null;
                allServicesNotInBouquetsToolStripMenuItem.Enabled = _settings != null;
            });
        }

        /// <summary>
        /// Displays Error dialog with message
        /// </summary>
        /// <param name="errorMsg"></param>
        private void ShowErrorDialog(string errorMsg)
        {
            AppSettings.Log.ErrorFormat("Displaying error dialog with error {0}", errorMsg);
            MessageBox.Show(errorMsg, Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Shows Question dialog 
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        private DialogResult ShowQuestionDialog(string question)
        {
            AppSettings.Log.ErrorFormat("Displaying question dialog with question {0}", question);
            return MessageBox.Show(question, Resources.Question, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        /// <summary>
        /// Displays Info dialog with message
        /// </summary>
        /// <param name="message"></param>
        private void ShowInfoDialog(string message)
        {
            AppSettings.Log.ErrorFormat("Displaying info dialog with message {0}", message);
            MessageBox.Show(message, Resources.Info, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        ///  Updates all child tree nodes recursively.
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="nodeChecked"></param>
        private void CheckAllChildNodes(TreeNode treeNode, bool nodeChecked)
        {
            foreach (TreeNode node in treeNode.Nodes)
            {
                node.Checked = nodeChecked;
                if (node.Nodes.Count > 0)
                {
                    // If the current node has child nodes, call the CheckAllChildsNodes method recursively.
                    CheckAllChildNodes(node, nodeChecked);
                }
            }
        }

        /// <summary>
        ///     Matches services from file bouquets to services from settings
        /// </summary>
        /// <remarks></remarks>
        /// <exception cref="SettingsException"></exception>
        public void MatchFileBouquetsServices(IList<ServiceWithBoquets> duplicates)
        {

            if (_settings == null)
            {
                AppSettings.Log.Debug("Settings are null, skip matching duplicate services to services from bouquets");
                return;
            }

            if (_settings.Bouquets.Count == 0)
            {
                AppSettings.Log.Debug("There is no bouquets, skip matching duplicate services to services from bouquets");
                return;
            }

            if (duplicates == null || duplicates.Count == 0)
            {
                AppSettings.Log.Debug("No duplicates, skip matching duplicate services to services from bouquets");
                return;
            }

            AppSettings.Log.Debug("Matching duplicate services with bouquets");

            try
            {

                foreach (var bouquet in _settings.Bouquets)
                {
                    var query = duplicates.Join(bouquet.BouquetItems.OfType<IBouquetItemService>(), bs => bs.Service.ServiceId, srv => srv.ServiceId.ToLower(), (bs, srv) => new
                    {
                        DuplicateItem = bs,
                        BiService = srv
                    });

                    foreach (var match in query.Where(x => x.BiService != null).ToList())
                    {
                        if (!match.DuplicateItem.Bouquets.Contains(bouquet))
                        {
                            match.DuplicateItem.Bouquets.Add(bouquet);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                AppSettings.Log.Error(ex.Message, ex);
                throw new SettingsException(Resources.There_was_an_error_while_matching_duplicates_with_bouquets, ex);
            }
        }

        /// <summary>
        /// Opens up open file dialog and loads settings
        /// </summary>
        private void LoadSettings()
        {
            using (var fd = new OpenFileDialog())
            {
                fd.CheckFileExists = true;
                fd.FileName = "lamedb";
                fd.Filter = @"Enigma2 settings|lamedb|Enigma1 settings|services|All Files (*.*)|*.*";
                fd.Multiselect = false;
                fd.Title = Resources.Choose_Settings_File;
                fd.Title = Resources.STATUS_SELECT_SERVICES;

                //check if user has canceled the action
                if (fd.ShowDialog() == DialogResult.Cancel)
                    return;
                OpenSettingsFileAsync(fd.FileName);
            }
        }

        /// <summary>
        /// Opens save dialog and saves settings to disk
        /// </summary>
        private void SaveSettings()
        {
            if (_settings == null)
                return;

            using (var fb = new FolderBrowserDialog())
            {
                fb.SelectedPath = _settings.SettingsDirectory;
                fb.Description = Resources.Choose_folder;
                if (fb.ShowDialog() == DialogResult.OK)
                {
                    var settingsIO = new SettingsIO();
                    settingsIO.SaveAsync(new DirectoryInfo(fb.SelectedPath), _settings, ar => ShowInfoDialog(Resources.Settings_saved_sucessfully_));
                }
            }
        }

        /// <summary>
        /// Removes selected duplicates from settings
        /// </summary>
        private void RemoveSelected()
        {
            if (_settings == null)
                return;

            AppSettings.Log.Debug("RemoveSelected started");
            var servicesForRemoval = new List<IService>();

            //loop trough satellites
            foreach (TreeNode satNode in treeView.Nodes)
            {
                //loop trough transponders
                foreach (TreeNode transNode in satNode.Nodes)
                {
                    //loop trough services
                    foreach (TreeNode servNode in transNode.Nodes)
                    {
                        if (servNode.Checked)
                        {
                            servicesForRemoval.Add(((ServiceWithBoquets)servNode.Tag).Service);
                        }
                    }
                }
            }

            if (servicesForRemoval.Any())
            {
                _settings.RemoveServices(servicesForRemoval);
                ShowInfoDialog(String.Format(Resources.Sucessfully_removed__0__services_, servicesForRemoval.Count()));
                FindDuplicatesAsync(_settings, FindDuplicatesCallback);
            }
            AppSettings.Log.Debug("RemoveSelected finished");
        }

        private void RemoveServicesNotInBouquets()
        {
            if (_settings == null)
            {
                AppSettings.Log.Debug("Settings are null, skip removing services not in Bouquets");
                return;
            }

            if (_settings.Bouquets.Count == 0)
            {
                AppSettings.Log.Debug("There is no bouquets, skip removing services not in Bouquets");
                return;
            }

            if (
                ShowQuestionDialog(
                    String.Format(
                        Resources.This_action_removes_not_only_duplicates_but_ALL_services_not_in_any_Bouquets__0_Are_you_sure_you_want_to_continue,
                        Environment.NewLine)) != DialogResult.Yes)
                return;

            AppSettings.Log.Debug("Removing services not in Bouquets");

            var bouquetItems =
                _settings.Bouquets.SelectMany(x => x.BouquetItems).OfType<IBouquetItemService>().Select(x => x.ServiceId.ToLower()).Distinct().ToList();

            var query = from service in _settings.Services
                        join bItem in bouquetItems on service.ServiceId.ToLower() equals bItem into gj
                        from subServ in gj.DefaultIfEmpty()
                        where subServ == null
                        select service;

            var servicesForRemoval = query.ToList();
            if (servicesForRemoval.Any())
            {
                _settings.RemoveServices(servicesForRemoval);
                ShowInfoDialog(String.Format(Resources.Sucessfully_removed__0__services_, servicesForRemoval.Count()));
                FindDuplicatesAsync(_settings, FindDuplicatesCallback);
            }
            AppSettings.Log.Debug("RemoveServicesNotInBouquets finished");

        }

        /// <summary>
        /// (Un)Checks all nodes
        /// </summary>
        /// <param name="isChecked"></param>
        private void CheckAll(bool isChecked)
        {
            foreach (TreeNode node in treeView.Nodes)
            {
                node.Checked = isChecked;
            }
        }

        #endregion

        #region "GUI Events"

        private void toolStripLoad_Click(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void toolStripSave_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void treeView_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {

        }

        private void treeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            CheckAllChildNodes(e.Node, e.Node.Checked);
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateInfoText();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void allDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckAll(true);
        }

        private void deselectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckAll(false);
        }

        private void duplicatesNotInBouquetsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            //loop trough satellites
            foreach (TreeNode satNode in treeView.Nodes)
            {
                bool satelliteChecked = false;
                satNode.Checked = true;

                //loop trough transponders
                foreach (TreeNode transNode in satNode.Nodes)
                {
                    bool transponderChecked = false;
                    transNode.Checked = true;

                    //loop trough services
                    foreach (TreeNode servNode in transNode.Nodes)
                    {
                        servNode.Checked = !((ServiceWithBoquets)servNode.Tag).Bouquets.Any();
                        if (servNode.Checked)
                        {
                            transponderChecked = true;
                            satelliteChecked = true;
                        }
                    }

                    if (!transponderChecked)
                        transNode.Checked = false;
                }
                if (!satelliteChecked)
                    satNode.Checked = false;
            }
        }

        private void selectedDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void allServicesNotInBouquetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveServicesNotInBouquets();
        }

        private void toolStripRemoveSelected_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void toolStripRemoveUnused_Click(object sender, EventArgs e)
        {
            RemoveServicesNotInBouquets();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About.DefInstance.ShowDialog();
        }

        #endregion

        #region "Load Settings"

        /// <summary>
        /// Starts loading settings file
        /// </summary>
        /// <param name="fileName"></param>
        private void OpenSettingsFileAsync(String fileName)
        {
            AppSettings.Log.DebugFormat("Initialized OpenSettingsFileAsync with filename {0}", fileName);
            _isLoadingList = true;
            UpdateControlsEnabled();
            SetProgressBarVisible(true);
            SetStatus(Resources.STATUS_READING_SERVICES);
            var settingsIO = new SettingsIO();
            settingsIO.LoadAsync(new FileInfo(fileName), OpenSettingsFileCallback);
            AppSettings.Log.DebugFormat("OpenSettingsFileAsync finished");
        }

        private void OpenSettingsFileCallback(IAsyncResult ar)
        {
            AppSettings.Log.Debug("OpenSettingsFileCallback initialized");

            _isLoadingList = false;
            SetProgressBarVisible(false);
            SetStatus(string.Empty);

            // Retrieve the delegate.
            var result = (AsyncResult)ar;
            var caller = (Func<FileInfo, ISettings>)result.AsyncDelegate;

            try
            {
                _settings = (Settings)caller.EndInvoke(ar);
                FindDuplicatesAsync(_settings, FindDuplicatesCallback);
            }
            catch (Exception ex)
            {
                SetTreeviewNodes(treeView, null);
                AppSettings.Log.Error(Resources.ERROR_FAILED_SERVICES, ex);
                ShowErrorDialog(String.Format(Resources.ERROR_FAILED_SERVICES + "{0}{1}", Environment.NewLine, ex.Message));
            }
            finally
            {
                UpdateControlsEnabled();
            }
            AppSettings.Log.Debug("OpenSettingsFileCallback finished");
        }

        #endregion

        #region "GUI Thread Safe Update"

        /// <summary>
        /// Checks if action requires invoking, invokes if neccessary and runs action
        /// </summary>
        /// <param name="control"></param>
        /// <param name="action"></param>
        private static void InvokeIfRequired(Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Update status text thread safe
        /// </summary>
        /// <param name="status"></param>
        private void SetStatus(string status)
        {
            InvokeIfRequired(statusStrip1, () =>
            {
                toolStripStatus.Text = !string.IsNullOrEmpty(status) ? status : @"http://www.krkadoni.com";
            });
        }

        /// <summary>
        /// Update progress bar visibillity thread safe
        /// </summary>
        /// <param name="visible"></param>
        private void SetProgressBarVisible(bool visible)
        {
            InvokeIfRequired(statusStrip1, () =>
            {
                toolStripProgressBar.Visible = visible;
            });
        }

        /// <summary>
        /// Updates treeview nodes thread safe
        /// </summary>
        /// <param name="tv"></param>
        /// <param name="transponderGroups"></param>
        private void SetTreeviewNodes(TreeView tv, IEnumerable<TransponderGroup> transponderGroups)
        {
            if (tv != null)
            {
                InvokeIfRequired(tv, () =>
                {
                    tv.Nodes.Clear();
                    if (transponderGroups != null)
                    {
                        var rootNode = new TreeNode();
                        foreach (var transponderGroup in transponderGroups)
                        {
                            var node = new TreeNode();
                            rootNode.Nodes.Add(node);
                            node.Text = transponderGroup.Description;
                            node.Tag = transponderGroup;
                            node.ImageIndex = 0;
                            node.SelectedImageIndex = 0;
                            foreach (var transWithServ in transponderGroup.Transponders)
                            {
                                var transponderNode = new TreeNode();
                                node.Nodes.Add(transponderNode);
                                transponderNode.Tag = transWithServ;
                                transponderNode.Text = transWithServ.Description;
                                transponderNode.ImageIndex = 1;
                                transponderNode.SelectedImageIndex = 1;
                                foreach (var serviceWithBoquets in transWithServ.Services)
                                {
                                    var serviceNode = new TreeNode();
                                    transponderNode.Nodes.Add(serviceNode);
                                    serviceNode.Tag = serviceWithBoquets;
                                    serviceNode.Text = serviceWithBoquets.Description;
                                    serviceNode.ImageIndex = 2;
                                    serviceNode.SelectedImageIndex = 2;
                                }
                            }
                        }

                        foreach (TreeNode node in rootNode.Nodes)
                        {
                            node.ImageIndex = 0;
                            tv.Nodes.Add(node);
                        }

                    }
                });
            }
        }

        /// <summary>
        /// Updates info label with information about currently selected node
        /// </summary>
        private void UpdateInfoText()
        {
            InvokeIfRequired(lbInfo, () =>
           {

               if (treeView.SelectedNode == null)
               {
                   if (_settings != null)
                   {
                       lbInfo.Text = String.Format(Resources.Total___0__services___1__duplicates__2__3_, _settings.Services.Count(), _totalDuplicates, String.Empty, String.Empty);
                       return;
                   }
                   lbInfo.Text = string.Empty;
                   return;
               }

               switch (treeView.SelectedNode.Level)
               {
                   case 0:
                       var group = (TransponderGroup)treeView.SelectedNode.Tag;
                       lbInfo.Text = String.Format(Resources.Total___0__services___1__duplicates__2__3_, _settings.Services.Count(), _totalDuplicates, String.Empty, String.Empty);
                       break;
                   case 1:
                       var trans = (TransponderWithServices)treeView.SelectedNode.Tag;
                       var dvbs = trans.Transponder as ITransponderDVBS;
                       if (dvbs != null)
                       {
                           var transponder = dvbs;
                           lbInfo.Text = String.Format("Frequency: {0} {1} / Symbolrate: {2} / System: {3} {8}" +
                                                       "FEC: {4} / Inversion: {5} / TSID: {6} / NID: {7}{8}",
                                                       transponder.Frequency, transponder.PolarizationType.ToString().Substring(0, 1),
                                                       transponder.SymbolRate,
                                                       transponder.SystemType,
                                                       transponder.FEC,
                                                       transponder.InversionType,
                                                       transponder.TSID,
                                                       transponder.NID,
                                                       Environment.NewLine);
                       }
                       else
                       {
                           lbInfo.Text = String.Format("Frequency: {0} / TSID: {1} / NID: {2}{3}",
                               trans.Frequency,
                               trans.TSID,
                               trans.NID, Environment.NewLine);
                       }

                       lbInfo.Text += String.Format(Resources.Duplicates___0_, trans.Services.Count);
                       break;
                   default:
                       var service = (ServiceWithBoquets)treeView.SelectedNode.Tag;
                       if (service.Bouquets.Any())
                       {
                           lbInfo.Text = String.Format(Resources.FrmDuplicates_UpdateInfoText_Bouquets___0__1_, String.Join(", ", service.Bouquets.OrderBy(x => x.Name).Select(x => x.Name).ToArray()), Environment.NewLine);
                       }
                       else
                       {
                           lbInfo.Text = String.Empty;
                       }

                       if (!string.IsNullOrEmpty(service.Flags))                      
                       {
                           var flags = new StringBuilder();
                           foreach (var flag in service.Service.FlagList)
                           {
                               switch (flag.FlagType)
                               {
                                       case Enums.FlagType.C:
                                       flags.AppendFormat("{0}: {1} / ", flag.CFlagType.ToString(), flag.FlagValue.Substring(2).TrimStart('0'));
                                       break;
                                       case Enums.FlagType.F:
                                       flags.AppendFormat(" {0}: {1} / ", flag.FFlagType.ToString(), flag.FlagValue.Substring(2).TrimStart('0'));
                                       break;
                               }
                           }
                           if (!String.IsNullOrEmpty(flags.ToString()))
                           {
                               lbInfo.Text += String.Format("PID: {0}", flags.ToString().Trim());     
                           }                           
                       }
                       break;
               }
           });
        }

        #endregion

        #region "Find Duplicates"

        private List<TransponderGroup> FindDuplicates(Settings settings)
        {
            AppSettings.Log.DebugFormat("FindDuplicates is initialized");

            var query =
                from el in settings.Services
                group el by new { Key = el.Transponder, Name = el.Name.ToLower() }
                    into grp
                    select new { key = grp.Key, count = grp.Count(), services = grp.ToList() };

            var duplicates = query.Where(x => x.count > 1).
                SelectMany(x => x.services).
                GroupBy(x => x.Transponder).
                Select(x => new TransponderWithServices(x.Key, x.OrderBy(y => y.Name).ToList())).ToList();

            MatchFileBouquetsServices(duplicates.SelectMany(x => x.Services).ToList());

            var satTransponders = duplicates.Where(x => x.Transponder.TransponderType == Enums.TransponderType.DVBS).Select(x => x).OrderBy(x => x.Frequency).ToList();
            var cableTransponders = duplicates.Where(x => x.Transponder.TransponderType == Enums.TransponderType.DVBC).ToList();
            var terTransponders = duplicates.Where(x => x.Transponder.TransponderType == Enums.TransponderType.DVBT).ToList();


            var result = satTransponders.
                GroupBy(x => ((ITransponderDVBS)x.Transponder).Satellite).
                OrderBy(x => Convert.ToInt32(x.Key.Position)).ToList().
                Select(x => new TransponderGroup
            {
                Description = x.Key.Name,
                Satellite = x.Key,
                Transponders = new List<TransponderWithServices>(x.ToList())
            }).ToList();

            if (cableTransponders.Count > 0)
            {
                result.Add(new TransponderGroup
                {
                    Description = Resources.DVB_C_Services,
                    Transponders = new List<TransponderWithServices>(cableTransponders)
                });
            }

            if (terTransponders.Count > 0)
            {
                result.Add(new TransponderGroup
                {
                    Description = Resources.DVB_T_Services,
                    Transponders = new List<TransponderWithServices>(terTransponders)
                });
            }

            _totalDuplicates = result.SelectMany(x => x.Transponders).SelectMany(x => x.Services).Count();
            if (_totalDuplicates == 0)
            {
                ShowInfoDialog(Resources.No_duplicates_found____);
            }
            AppSettings.Log.Debug("FindDuplicates finished");
            return result;
        }

        private void FindDuplicatesAsync(Settings settings, AsyncCallback callback)
        {
            AppSettings.Log.DebugFormat("FindDuplicatesAsync is initialized");
            _isSearchingDuplicates = true;
            UpdateControlsEnabled();
            SetProgressBarVisible(true);
            new Func<Settings, List<TransponderGroup>>(FindDuplicates).BeginInvoke(settings, callback, null);
            AppSettings.Log.Debug("FindDuplicatesAsync finished");
        }

        private void FindDuplicatesCallback(IAsyncResult ar)
        {
            AppSettings.Log.Debug("FindDuplicatesCallback initialized");
            _isSearchingDuplicates = false;
            SetProgressBarVisible(false);
            SetStatus(string.Empty);

            // Retrieve the delegate.
            var result = (AsyncResult)ar;
            var caller = (Func<Settings, List<TransponderGroup>>)result.AsyncDelegate;

            try
            {
                var transponderGroups = caller.EndInvoke(ar);
                SetTreeviewNodes(treeView, transponderGroups);
            }
            catch (Exception ex)
            {
                SetTreeviewNodes(treeView, null);
                AppSettings.Log.ErrorFormat("FindDuplicatesCallback invoked Exception");
                AppSettings.Log.Error(ex);
                ShowErrorDialog(String.Format(Resources.Error_occured_while_looking_for_duplicates_ + "{0}{1}", Environment.NewLine, ex.Message));
            }
            finally
            {

                UpdateControlsEnabled();
                UpdateInfoText();
            }
            AppSettings.Log.Debug("FindDuplicatesCallback finished");
        }

        #endregion

    }
}
