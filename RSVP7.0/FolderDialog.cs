using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms.Design;
using System.Windows.Forms;

namespace RSVP7._0
{
    class FolderDialog : FolderNameEditor   //注意使用FolderNameEditor要添加引用System.Design
    {
        FolderNameEditor.FolderBrowser fDialog = new
        System.Windows.Forms.Design.FolderNameEditor.FolderBrowser();
        public FolderDialog()
        { 
        }
        public DialogResult DisplayDialog()
        {
            return DisplayDialog("请选择一个文件夹");
        }
        public DialogResult DisplayDialog(string description)
        {
            fDialog.Description = description;
            return fDialog.ShowDialog();
        }
        public string Path
        {
            get
            {
                return fDialog.DirectoryPath;
            }
        }
        ~FolderDialog()
        {
            fDialog.Dispose();
        }
    }
}
