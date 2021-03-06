using System;
using System.IO;
using Gdk;
using Gtk;
using Glade;
using LibGPhoto2;
using Mono.Unix;

namespace FSpot {
	public class CameraFileSelectionDialog : GladeDialog
	{
		private const int DirectoryColumn = 0;
		private const int FileColumn = 1;
		private const int PreviewColumn = 2;
		private const int IndexColumn = 3;
		
		[Widget] Gtk.Button copyButton;
		[Widget] Gtk.Button cancelButton;
		[Widget] Gtk.Label selected_camera_name;
		[Widget] Gtk.TreeView file_tree;
		[Widget] Gtk.OptionMenu tag_option_menu;
		[Widget] Gtk.CheckButton attach_check;

		GPhotoCamera camera;
		ListStore preview_list_store;
		Db db;
		
		FSpot.ThreadProgressDialog progress_dialog;
		System.Collections.ArrayList index_list;
		
		string[] saved_files;
		Tag[] selected_tags;
		
		System.Threading.Thread command_thread;
		
		public CameraFileSelectionDialog (GPhotoCamera cam, Db datab)
		{
			camera = cam;
			db = datab;
			saved_files = null;
			selected_tags = null;
		}
		
		public void Run ()
		{	
			CreateInterface ();
			
			ResponseType response = (ResponseType) this.Dialog.Run ();
			if (response == ResponseType.Ok)
				if (SaveFiles ())
					ImportFiles ();
			
			this.Dialog.Destroy ();
			return;
		}
		
		private void CreateInterface ()
		{
			this.CreateDialog ("camera_file_selection_dialog");
			
			file_tree.Selection.Mode = SelectionMode.Multiple;
			file_tree.AppendColumn (Catalog.GetString ("Preview"), 
						new CellRendererPixbuf (), "pixbuf", PreviewColumn);
			file_tree.AppendColumn (Catalog.GetString ("Path"), 
						new CellRendererText (), "text", DirectoryColumn);
			file_tree.AppendColumn (Catalog.GetString ("File"), 
						new CellRendererText (), "text", FileColumn);
			file_tree.AppendColumn (Catalog.GetString ("Index"),
						new CellRendererText (), "text", IndexColumn).Visible = false;
			
			preview_list_store = new ListStore (typeof (string), typeof (string), 
							    typeof (Pixbuf), typeof (int));
			
			file_tree.Model = preview_list_store;

			CreateTagMenu ();
			attach_check.Toggled += HandleTagToggled;
			HandleTagToggled (null, null);
			
			GetPreviews ();
		}
		
		private void CreateTagMenu ()
		{
			TagMenu tagmenu = new TagMenu (null, MainWindow.Toplevel.Database.Tags);
			tagmenu.NewTagHandler = HandleNewTagSelected;
			
			tagmenu.Append (new MenuItem (Catalog.GetString ("Select Tag")));
			
			tagmenu.Populate (true);
			
			tagmenu.TagSelected += HandleTagMenuSelected;
			
			tagmenu.ShowAll ();
			tag_option_menu.Menu = tagmenu;
		}

		private void HandleTagMenuSelected (Tag t) 
		{
			selected_tags = new Tag [] { t };
		}

		private void HandleNewTagSelected (object sender, EventArgs args)
		{
			Tag new_tag = MainWindow.Toplevel.CreateTag (Dialog, null);
			
			if (new_tag != null) {
				CreateTagMenu ();
				tag_option_menu.SetHistory ((uint) (tag_option_menu.Menu as TagMenu).GetPosition (new_tag));
				selected_tags = new Tag [] { new_tag };
			}
		}
		
		public void HandleTagToggled (object o, EventArgs args)
		{
			tag_option_menu.Sensitive = attach_check.Active;
			if (!attach_check.Active)
				selected_tags = null;
		}

		private void GetPreviews ()
		{
			lock (camera) {
				ProgressDialog pdialog = new ProgressDialog (Catalog.GetString ("Downloading Previews"), 
									     ProgressDialog.CancelButtonType.Cancel, 
									     camera.FileList.Count, 
									     this.Dialog);
				
				int index = 0;
				bool load_thumb = true;
				foreach (GPhotoCameraFile file in camera.FileList) {
					string msg = String.Format (Catalog.GetString ("Downloading Preview of {0}"), 
								    file.FileName);
					
					
					
					if (load_thumb && pdialog.Update (msg)) {
						load_thumb = false;
						pdialog.Hide ();
					}
					
					Pixbuf scale = null;
					if (load_thumb) {
						Pixbuf thumbscale = camera.GetPreviewPixbuf (file);
						if (thumbscale != null) {
							scale = PixbufUtils.ScaleToMaxSize (thumbscale, 64,64);
							thumbscale.Dispose ();
						}
					}
					
					preview_list_store.AppendValues (file.Directory, file.FileName, scale, index);
					index++;
				}
				
				file_tree.Selection.SelectAll ();
				pdialog.Destroy ();
			}
		}
		
		private System.Collections.ArrayList GetSelectedItems ()
		{
			TreeSelection selection = file_tree.Selection;
			TreeModel model;
			TreePath[] selected_rows = selection.GetSelectedRows (out model);
			
			System.Collections.ArrayList list = new System.Collections.ArrayList ();
			foreach (TreePath cur_row in selected_rows) {
				TreeIter cur_iter;
				
				if (model.GetIter (out cur_iter, cur_row))
					list.Add ((int) model.GetValue (cur_iter, IndexColumn));
			}
			
			return list;
		}
		
		private bool SaveFiles ()
		{
			index_list = GetSelectedItems ();
			this.Dialog.Hide ();
			
			command_thread = new System.Threading.Thread (new System.Threading.ThreadStart (this.Download));
			command_thread.Name = Catalog.GetString ("Transferring Pictures");
			
			progress_dialog = new FSpot.ThreadProgressDialog (command_thread, 1);
			progress_dialog.Start ();
			
			while (command_thread.IsAlive) {
				if (Application.EventsPending ())
					Application.RunIteration ();
				
			}
			
			return true;
		}

		private void Download ()
		{
			lock (camera) {
				System.Collections.ArrayList saved = new System.Collections.ArrayList ();
					
				int count = 0;
				for (int i = 0; i < index_list.Count; i++) {
					try {
						count++;
						string msg = String.Format (Catalog.GetString ("Copying file {0} of {1}"),
									    i, index_list.Count);
						
						progress_dialog.ProgressText = msg;
						saved.Add (SaveFile ((int)(index_list [i])));
						progress_dialog.Fraction = count/(double)index_list.Count;
					}
					catch (System.Exception e) {
						System.Console.WriteLine (e.ToString ());
						progress_dialog.Message = String.Format ("{0}{2}{1}", e.Message, e.ToString (), Environment.NewLine);
						progress_dialog.ProgressText = Catalog.GetString ("Error transferring file");

						if (progress_dialog.PerformRetrySkip ())
						 	i--;
					}
				}
					
				saved_files = (string []) saved.ToArray (typeof (string));
					
				progress_dialog.Message = Catalog.GetString ("Done Copying Files");
				progress_dialog.Fraction = 1.0;
				progress_dialog.ProgressText = Catalog.GetString ("Download Complete");
				progress_dialog.ButtonLabel = Gtk.Stock.Ok;
			}
		}
		
		private string SaveFile (int index) 
		{
			GPhotoCameraFile camfile = (GPhotoCameraFile) camera.FileList [index];
			string tempdir = FSpot.Global.PhotoDirectory;
			if (! Directory.Exists (tempdir))
				Directory.CreateDirectory (tempdir);

			string orig = Path.Combine (tempdir, camfile.FileName.ToLower ());
			string path = orig;
		
			int i = 0;
			while (File.Exists (path)) {
				string name = String.Format ("{0}-{1}{2}", 
							     Path.GetFileNameWithoutExtension (orig), 
							     i, Path.GetExtension (orig));
				
				path = System.IO.Path.Combine (Path.GetDirectoryName (orig), name);
				i++;
			}
			
			string msg = String.Format (Catalog.GetString ("Transferring \"{0}\" from camera"), 
						    Path.GetFileName (path));
			progress_dialog.Message = msg;
			
			camera.SaveFile (index, path);
			
			string dest = FileImportBackend.ChooseLocation (path);
			System.IO.File.Move (path, dest);
			path = dest;

			return path;
		}
		
		private void ImportFiles ()
		{
			ImportCommand command = new ImportCommand (null);
			command.ImportFromPaths (db.Photos, saved_files, selected_tags);
		}
		
		public Tag[] Tags {
			get {
				return selected_tags;
			}
		}
	}
}
