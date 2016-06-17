using Domino;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HANDLE = System.UInt32;
using STATUS = System.UInt16;

namespace ArduinoPerfMeterGui
{
    internal class NotesMailChecker
    {
        private NotesDatabase db;
        private HANDLE hNotesDB;
        private HANDLE hUnreadListTable;
        private string mailFile = "";
        private string mailServer = "";
        private NotesSession ns;
        private ProgramState programState;
        private string userName = "";
        private List<HANDLE> unreadMailList;

        public NotesMailChecker()
        {
            programState = ProgramState.Start;

            RaymondsInit();
        }

        private enum ProgramState
        {
            Start,
            Initializing,
            Connected,
            Disconnected,
            Waiting,
            End
        }

        [DllImport("nnotes.dll")]
        public static extern STATUS IDDestroyTable(HANDLE h);

        [DllImport("nnotes.dll")]
        public static extern bool IDScan(HANDLE hTable, bool first, out HANDLE id);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbClose(HANDLE hDb);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbGetUnreadNoteTable(HANDLE hDb, string user, ushort namelen, bool create, out HANDLE hList);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbOpen(string path, out HANDLE phDB);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbUpdateUnread(HANDLE hDb, HANDLE hUnreadList);

        [DllImport("nnotes.dll")]
        public static extern STATUS OSMemFree(HANDLE h);

        [DllImport("nnotes.dll")]
        public static extern STATUS OSPathNetConstruct(string port, string server, string filename, StringBuilder retFullpathName);

        // Return number of unread mail.
        public int GetUnreadMail()
        {
            List<HANDLE> a = GetUnreadMailList();
            if (a == null) return -1;
            unreadMailList = a;
            return unreadMailList.Count;
        }

        // Return number of unread mail with ID not in the previous unread ID list. FALSE otherwise.
        public int GetNewUnreadMail()
        {
            List<HANDLE> a = GetUnreadMailList();
            if (a == null) return -1;
            // Find the set {a}-{unreadMailList}, i.e. new unread mail.
            List<HANDLE> b = new List<HANDLE>();
            foreach (HANDLE id in a)
                if (!unreadMailList.Contains(id))
                    b.Add(id);
            unreadMailList = a;
            return b.Count;
        }

        // Return a List<HANDLE> of unread mail. 
        private List<HANDLE> GetUnreadMailList()
        {
            Debug.WriteLine($"[GetUnreadMail] Program status: {programState.ToString()}");
            if (programState != ProgramState.Connected)     // Retry connecting to Domino once, if not connected.
            {
                if (!OpenNotesDatabase()) return null;
            }

            bool first = true;
            HANDLE id;
            NSFDbUpdateUnread(hNotesDB, hUnreadListTable);
            List<HANDLE> mailList = new List<HANDLE>();
            while (IDScan(hUnreadListTable, first, out id))
            {
                Debug.WriteLine($" trying to fetch email of ID {id.ToString("X")}");
                NotesDocument doc = db.GetDocumentByID(id.ToString("X"));
                string subject = (string)((object[])doc.GetItemValue("Subject"))[0];
                string sender = (string)((object[])doc.GetItemValue("From"))[0];
                if (!sender.Equals(""))
                {
                    Debug.WriteLine($"   Doc: {subject} / *{sender}*");
                    // if (!sender.Equals(userName))        // To filter email sent by myself.
                        mailList.Add(id);
                }
                first = false;
            }
            return mailList;
        }

        private void CloseNotesDatabase(HANDLE hDb)
        {
            if (hUnreadListTable != 0) { IDDestroyTable(hUnreadListTable); hUnreadListTable = 0; }
            if (hDb != 0) { NSFDbClose(hNotesDB); hNotesDB = 0; }
            programState = ProgramState.Disconnected;
        }

        private bool OpenNotesDatabase()
        {
            HANDLE hDb = 0;

            try
            {
                this.ns = new NotesSession();
                this.ns.Initialize("password");
                this.mailServer = this.ns.GetEnvironmentString("MailServer", true);
                this.mailFile = this.ns.GetEnvironmentString("MailFile", true);
                this.userName = this.ns.UserName;
                Debug.WriteLine($"mailServer: {mailServer}");
                Debug.WriteLine($"mailFile: {mailFile}");
                Debug.WriteLine($"userName: {userName}");

                this.db = this.ns.GetDatabase(mailServer, mailFile, false);
                StringBuilder fullpathName = new StringBuilder(512);
                OSPathNetConstruct(null, mailServer, mailFile, fullpathName);
                Debug.WriteLine($"fullpathName: {fullpathName.ToString()}");

                NSFDbOpen(fullpathName.ToString(), out hDb);
                NSFDbGetUnreadNoteTable(hDb, userName, (ushort)userName.Length, true, out hUnreadListTable);
                Debug.WriteLine($"hDb: {hDb.ToString()}");
                Debug.WriteLine($"hUnreadListTable: {hUnreadListTable.ToString()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] OpenNotesDatabase: {ex.ToString()}");
            }
            if (hDb != 0 && hUnreadListTable != 0)
            {
                programState = ProgramState.Connected;
                hNotesDB = hDb; return true;
            }
            else
            {
                CloseNotesDatabase(hDb);
                return false;
            }
        }

        public void clearMailList()
        {
            this.unreadMailList.Clear();


        }

        private void RaymondsInit()
        {
            programState = ProgramState.Initializing;
            unreadMailList = new List<HANDLE>();
            unreadMailList.Clear();
            OpenNotesDatabase();
        }
    }
}