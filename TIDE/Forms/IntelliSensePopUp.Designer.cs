﻿using System;
using System.Windows.Forms;

namespace TIDE.Forms
{
    partial class IntelliSensePopUp
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Items = new System.Windows.Forms.ListBox();
            SuspendLayout();
            // 
            // Items
            // 
            this.Items.BackColor = System.Drawing.Color.FromArgb(42, 42, 42);
            this.Items.BorderStyle = BorderStyle.None;
            this.Items.Dock = DockStyle.Fill;
            this.Items.ForeColor = System.Drawing.Color.White;
            this.Items.FormattingEnabled = true;
            this.Items.Location = new System.Drawing.Point(0, 0);
            this.Items.Name = "Items";
            this.Items.Size = new System.Drawing.Size(152, 64);
            this.Items.TabIndex = 0;
            this.Items.MouseDoubleClick += new MouseEventHandler(this.Items_MouseDoubleClick);
            this.Items.PreviewKeyDown += new PreviewKeyDownEventHandler(this.Items_PreviewKeyDown);
            // 
            // IntelliSensePopUp
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(42)))), ((int)(((byte)(42)))));
            this.ClientSize = new System.Drawing.Size(152, 64);
            this.ControlBox = false;
            this.Controls.Add(this.Items);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "IntelliSensePopUp";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.ResumeLayout(false);
        }

        #endregion

        private ListBox Items;
    }
}