using System.IO;
using System;
using Mono.Unix;
using NDesk.DBus;
using org.freedesktop.DBus;

namespace FSpot {
	[Interface ("org.gnome.FSpot.Core")]
	public interface ICore {
		void Import (string path);

		void Organize ();
		
		void View (string list);

		void Shutdown ();
	}

	public class Core : ICore
	{
		MainWindow organizer;
		private static Db db;
		System.Collections.ArrayList toplevels;
		const string ServicePath = "org.gnome.FSpot";
		static ObjectPath CorePath = new ObjectPath ("/org/gnome/FSpot/Core");

		public Core () : this (false)
		{
		}

		public Core (bool nodb)
		{
			toplevels = new System.Collections.ArrayList ();
			
		}

		public static Db Database {
			get { 
				if (db == null) {
					// Load the database, upgrading/creating it as needed
					string base_directory = FSpot.Global.BaseDirectory;
					if (! File.Exists (base_directory))
						Directory.CreateDirectory (base_directory);
					
					db = new Db ();
					db.Init (Path.Combine (base_directory, "photos.db"), true);
				}
				return db; 
			}
		}

		public static ICore FindInstance ()
		{
			if (Bus.Session.NameHasOwner (ServicePath)) {
				return Bus.Session.GetObject<ICore> (ServicePath, CorePath);
			} else {
				throw new Exception("No Instance Found");
			}
		}
		
		public void UnregisterServer ()
		{
			Bus.Session.ReleaseName(ServicePath);
		}

		public void RegisterServer ()
		{
			RequestNameReply nameReply = Bus.Session.RequestName (ServicePath, NameFlag.DoNotQueue);
#if DEBUG_DBUS
			Console.WriteLine("RequestNameReply {0}", nameReply);
#endif
			Bus.Session.Register (ServicePath, CorePath, this);
		}
		
		private class ImportCommand 
		{
			string path;
			MainWindow main;

			public ImportCommand (MainWindow main, string path) 
			{
				this.main = main;
				this.path = path;
			}

			public bool Execute ()
			{
				if (path != null && path.StartsWith ("gphoto2:"))
					main.ImportCamera (path);
				else
					main.ImportFile (path);
				
				return false;
			}
		}

		public void Import (string path) 
		{
			ImportCommand cmd = new ImportCommand (MainWindow, path);
			cmd.Execute ();
		}

		public MainWindow MainWindow {
			get {
				if (organizer == null) {
					organizer = new MainWindow (Database);
					Register (organizer.Window);
				}
				
				return organizer;
			}
		}
			
		public void Organize ()
		{
			MainWindow.Window.Present ();
		}
		
		public void View (string list)
		{
			Viewbla (new UriList (list));
		}

		public void Viewbla (UriList list)
		{
			try {
				Register (new FSpot.SingleView (list).Window);
			} catch (System.Exception e) {
				System.Console.WriteLine (e.ToString ());
				System.Console.WriteLine ("no real valid path to view from {0}", list);
			} 
		}
		
		private class SlideShow
		{
			SlideView slideview;
			Gtk.Window window;
			
			public Gtk.Window Window {
				get { return window; }
			}

			public SlideShow (string name)
			{
				Tag tag;
				
				if (name != null)
					tag = Database.Tags.GetTagByName (name);
				else {
					int id = (int) Preferences.Get (Preferences.SCREENSAVER_TAG);
					tag = Database.Tags.GetTagById (id);
				}
				
				Photo [] photos;
				if (tag != null)
					photos = Database.Photos.Query (new Tag [] { tag } );
 				else if ((int) Preferences.Get (Preferences.SCREENSAVER_TAG) == 0)
 					photos = db.Photos.Query (new Tag [] {});
				else
					photos = new Photo [0];

				window = new XScreenSaverSlide ();
				SetStyle (window);
				if (photos.Length > 0) {
					Array.Sort (photos, new Photo.RandomSort ());
					
					Gdk.Pixbuf black = new Gdk.Pixbuf (Gdk.Colorspace.Rgb, false, 8, 1, 1);
					black.Fill (0x00000000);
					slideview = new SlideView (black, photos);
					window.Add (slideview);
				} else {
					Gtk.HBox outer = new Gtk.HBox ();
					Gtk.HBox hbox = new Gtk.HBox ();
					Gtk.VBox vbox = new Gtk.VBox ();

					outer.PackStart (new Gtk.Label (String.Empty));
					outer.PackStart (vbox, false, false, 0);
					vbox.PackStart (new Gtk.Label (String.Empty));
					vbox.PackStart (hbox, false, false, 0);
					hbox.PackStart (new Gtk.Image (Gtk.Stock.DialogWarning, Gtk.IconSize.Dialog),
							false, false, 0);
					outer.PackStart (new Gtk.Label (String.Empty));

					string msg;
					string long_msg;

					if (tag != null) {
						msg = String.Format (Catalog.GetString ("No photos matching {0} found"), tag.Name);
						long_msg = String.Format (Catalog.GetString ("The tag \"{0}\" is not applied to any photos. Try adding\n" +
											     "the tag to some photos or selecting a different tag in the\n" +
											     "F-Spot preference dialog."), tag.Name);
					} else {
						msg = Catalog.GetString ("Search returned no results");
						long_msg = Catalog.GetString ("The tag F-Spot is looking for does not exist. Try\n" + 
									      "selecting a different tag in the F-Spot preference\n" + 
									      "dialog.");
					}

					Gtk.Label label = new Gtk.Label (msg);
					hbox.PackStart (label, false, false, 0);

					Gtk.Label long_label = new Gtk.Label (long_msg);
					long_label.Markup  = String.Format ("<small>{0}</small>", long_msg);

					vbox.PackStart (long_label, false, false, 0);
					vbox.PackStart (new Gtk.Label (String.Empty));

					window.Add (outer);
					SetStyle (label);
					SetStyle (long_label);
					//SetStyle (image);
				}
				window.ShowAll ();
			}

			private void SetStyle (Gtk.Widget w) 
			{
				w.ModifyFg (Gtk.StateType.Normal, new Gdk.Color (127, 127, 127));
				w.ModifyBg (Gtk.StateType.Normal, new Gdk.Color (0, 0, 0));
			}

			public bool Execute ()
			{
				if (slideview != null)
					slideview.Play ();
				return false;
			}
		}

		public void ShowSlides (string name)
		{
			SlideShow show = new SlideShow (name);
			Register (show.Window);
			GLib.Idle.Add (new GLib.IdleHandler (show.Execute));
		}


		public void Shutdown ()
		{
			try {
				MainWindow.Toplevel.Close ();
			} catch {
				System.Environment.Exit (0);
			}
		}

		public void Register (Gtk.Window window)
		{
			toplevels.Add (window);
			window.Destroyed += HandleDestroyed;
		}

		public void HandleDestroyed (object sender, System.EventArgs args)
		{
			toplevels.Remove (sender);
			if (toplevels.Count == 0) {
				// FIXME
				// Should use Application.Quit(), but for that to work we need to terminate the threads
				// first too.
				System.Environment.Exit (0);
			}
			if (organizer != null && organizer.Window == sender)
				organizer = null;
		}
	}
}
