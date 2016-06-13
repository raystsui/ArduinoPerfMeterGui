using Domino;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HANDLE = System.UInt32;
using STATUS = System.UInt16;
using System.Collections.Generic;

namespace ArduinoPerfMeterGui
{
    class NotesMailChecker
    {
        private enum ProgramState
        {
            Start,
            Initializing,
            Connected,
            Disconnected,
            Waiting,
            End
        }

        private ProgramState programState;
        private NotesSession ns;
        private NotesDatabase db;
        private HANDLE hNotesDB;
        private HANDLE hUnreadListTable;
        private string mailServer = "";
        private string mailFile = "";
        private string userName = "";

        [DllImport("nnotes.dll")]
        public static extern STATUS OSPathNetConstruct(string port, string server, string filename, StringBuilder retFullpathName);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbOpen(string path, out HANDLE phDB);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbClose(HANDLE hDb);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbGetUnreadNoteTable(HANDLE hDb, string user, ushort namelen, bool create, out HANDLE hList);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbUpdateUnread(HANDLE hDb, HANDLE hUnreadList);

        //[DllImport("nnotes.d ll")]
        //public static extern bool IDIsPresent(HANDLE hTable, DWORD id);
        [DllImport("nnotes.dll")]
        public static extern bool IDScan(HANDLE hTable, bool first, out HANDLE id);

        [DllImport("nnotes.dll")]
        public static extern STATUS OSMemFree(HANDLE h);

        [DllImport("nnotes.dll")]
        public static extern STATUS IDDestroyTable(HANDLE h);

        public NotesMailChecker()
        {
            programState = ProgramState.Start;

            RaymondsInit();
        }

        private void RaymondsInit()
        {
            programState = ProgramState.Initializing;
            hNotesDB = OpenNotesDatabase();
        }

        private HANDLE OpenNotesDatabase()
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
                Debug.WriteLine($"hNotesDB: {hNotesDB.ToString()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenNotesDatabase: {ex.ToString()}");
            }
            if (hDb != 0)
            {
                programState = ProgramState.Connected;
            }

            return hDb;
        }

        private void CloseNotesDatabase(HANDLE hDb)
        {
            if (hUnreadListTable != 0) { IDDestroyTable(hUnreadListTable); hUnreadListTable = 0; }
            if (hDb != 0) { NSFDbClose(hNotesDB); hNotesDB = 0; }
            programState = ProgramState.Disconnected;
        }

        // Return number of unread mail.
        public int GetUnreadMail()
        {
            int result = -1;
            bool first = true;
            int triedReconnect = 0;
            HANDLE id;

            if (programState != ProgramState.Connected)
            {
                if (triedReconnect < 1)
                {
                    triedReconnect++;
                    hNotesDB = OpenNotesDatabase();

                }

                return -1;

            }

            while (IDScan(hUnreadListTable, first, out id))
            {
                NotesDocument doc = db.GetDocumentByID(id.ToString("X"));
                string subject = (string)((object[])doc.GetItemValue("Subject"))[0];
                string sender = (string)((object[])doc.GetItemValue("From"))[0];
                if (!sender.Equals(""))
                {
                    Debug.WriteLine($"   Doc: {subject} / *{sender}*");
                    if (!sender.Equals(userName))
                        result++;
                }
                first = false;
            }

            return result;
        }

        // Return number of unread mail with ID not in the previous unread ID list. FALSE otherwise.
        public int GetNewUnreadMail()
        {
            int result = -1;
            return result;
        }
    }
}