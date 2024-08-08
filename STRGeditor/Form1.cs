using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;

namespace STRGeditor
{
    public partial class Form1 : Form
    {
        string[][] textos;

        uint bigendian(uint le)
        {
            uint pr = le >> 24;
            uint se = le >> 8 & 0x00FF00;
            uint te = le << 24;
            uint qu = le << 8 & 0x00FF0000;
            return pr | se | te | qu;
        }

        byte[] ntc;
        uint magic;
        uint version;
        uint langnumb;
        uint textnumb;
        uint nametable;
        uint nts;

        string arquivo_aberto;

        public Form1()
        {
            InitializeComponent();
        }

        private void abrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "DKCR or MP 3 STRG|*.STRG|All files (*.*)|*.*";
            openFileDialog1.Title = "Select a STRG File";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                ResetarEstado();

                arquivo_aberto = openFileDialog1.FileName;

                FileStream arq = new FileStream(openFileDialog1.FileName, FileMode.Open);
                BinaryReader br = new BinaryReader(arq);

                magic = bigendian(br.ReadUInt32());
                version = bigendian(br.ReadUInt32());
                langnumb = bigendian(br.ReadUInt32());
                textnumb = bigendian(br.ReadUInt32());
                nametable = bigendian(br.ReadUInt32());
                nts = bigendian(br.ReadUInt32());
                dataGridView1.Rows.Clear();
                uint val = nts + 0x18;
                ntc = br.ReadBytes((int)nts);

                listBox1.Items.Clear();

                arq.Seek(val, SeekOrigin.Begin);

                for (int i = 0; i < langnumb; i++)
                {
                    byte[] btexto = br.ReadBytes(4);
                    string texto = Encoding.ASCII.GetString(btexto);
                    listBox1.Items.Add(texto);
                }

                textos = new string[langnumb][];

                for (int i = 0; i < langnumb; i++)
                {
                    textos[i] = new string[textnumb];
                    uint str_block_len = bigendian(br.ReadUInt32());

                    for (int j = 0; j < textnumb; j++)
                    {
                        uint prpt = bigendian(br.ReadUInt32());

                        long pos_antiga = arq.Position;

                        uint primeirotexto = (4 + textnumb * 4) * langnumb + val + langnumb * 4;

                        arq.Seek(primeirotexto + prpt, SeekOrigin.Begin);
                        byte[] textg = br.ReadBytes((int)bigendian(br.ReadUInt32()) - 1);
                        string textt = Encoding.UTF8.GetString(textg);

                        arq.Seek(pos_antiga, SeekOrigin.Begin);

                        textos[i][j] = textt;
                    }
                }

                br.Close();
                arq.Close();
            }

            openFileDialog1.Dispose();
        }

        private void ResetarEstado()
        {
            dataGridView1.Rows.Clear();
            listBox1.Items.Clear();
            textos = null;
            arquivo_aberto = null;
            magic = 0;
            version = 0;
            langnumb = 0;
            textnumb = 0;
            nametable = 0;
            nts = 0;
            ntc = null;
            old_index = -1;
        }

        private void arquivoToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        int old_index = -1;

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex > -1)
            {
                salvar();

                dataGridView1.Rows.Clear();

                for (int i = 0; i < textos[listBox1.SelectedIndex].Length; i++)
                {
                    dataGridView1.Rows.Add(textos[listBox1.SelectedIndex][i]);
                }

                old_index = listBox1.SelectedIndex;
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            salvar_arq(arquivo_aberto);
        }

        private void salvarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            salvar_arq_com_dialogo(false);
        }

        private void salvarComoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            salvar_arq_com_dialogo(true);
        }

        private void salvar_arq_com_dialogo(bool Comprimir)
        {
            SaveFileDialog SaveFileDialog1 = new SaveFileDialog();
            SaveFileDialog1.Filter = "DKCR or MP 3 STRG|*.STRG|All files (*.*)|*.*";
            SaveFileDialog1.Title = "Save a STRG File";

            if (SaveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                salvar_arq(SaveFileDialog1.FileName, Comprimir);
            }
        }

        private void salvar_arq(string FileName, bool Comprimir = false)
        {
            salvar();

            FileStream arq = new FileStream(FileName, FileMode.Create);

            BinaryWriter bw = new BinaryWriter(arq);

            bw.Write(bigendian(magic));
            bw.Write(bigendian(version));
            bw.Write(bigendian(langnumb));
            bw.Write(bigendian(textnumb));
            bw.Write(bigendian(nametable));
            bw.Write(bigendian(nts));
            bw.Write(ntc);

            for (int i = 0; i < langnumb; i++)
            {
                bw.Write(Encoding.ASCII.GetBytes(listBox1.Items[i].ToString()));
            }

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw2 = new BinaryWriter(ms);

            for (int i = 0; i < textos.Length; i++)
            {
                uint tamanho_strings = 0;

                for (int j = 0; j < textos[i].Length; j++)
                {
                    byte[] texto_utf8 = Encoding.UTF8.GetBytes(textos[i][j]);

                    tamanho_strings += (uint)texto_utf8.Length + 1;
                }

                bw.Write(bigendian(tamanho_strings));

                for (int j = 0; j < textos[i].Length; j++)
                {
                    bw.Write(bigendian((uint)ms.Position));
                    byte[] texto_bytes = Encoding.UTF8.GetBytes(textos[i][j]);
                    bw2.Write(bigendian((uint)texto_bytes.Length + 1));
                    bw2.Write(texto_bytes);
                    bw2.Write('\0');
                }
            }

            bw.Write(ms.ToArray());
            ms.Dispose();

            arq.Dispose();

            if (Comprimir) File.WriteAllBytes(FileName, Compress(File.ReadAllBytes(FileName)));
        }
        
        private void salvar()
        {
            if (old_index > -1)
            {

                for (int i = 0; i < textos[0].Length; i++)
                {
                    string val = (string)dataGridView1.Rows[i].Cells[0].Value;

                    if (val == null) val = string.Empty;

                    textos[old_index][i] = val;
                }
            }
        }


        private byte[] Compress(byte[] Data)
        {
            using (MemoryStream Stream = new MemoryStream())
            {
                DeflateStream Compressor = new DeflateStream(Stream, CompressionLevel.Optimal);
                Compressor.Write(Data, 0, Data.Length);
                Compressor.Close();

                using (MemoryStream NewStream = new MemoryStream())
                {
                    BinaryWriter Writer = new BinaryWriter(NewStream);
                    Writer.Write((ushort)0xda78);
                    Writer.Write(Stream.ToArray());
                    Writer.Write(bigendian(Adler32(Data)));
                    return NewStream.ToArray();
                }
            }
        }

        private uint Adler32(byte[] Data)
        {
            const int MOD_ADLER = 65521;

            uint a = 1, b = 0;
            for (int Index = 0; Index < Data.Length; Index++)
            {
                a = (a + Data[Index]) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }

            return (b << 16) | a;
        }

        private void sobreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("STRG editor created by Angel333119");
        }
    }
}
