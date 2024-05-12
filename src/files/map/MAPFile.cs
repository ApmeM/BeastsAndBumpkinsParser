using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SimpleTiled;
using SpriteViewer;

namespace BBData
{
    public class MAPFile : IFile
    {
        public string FileName;
        public TmxMap map;

        public MAPFile(string fileName, byte[] data)
        {
            FileName = fileName;
            using var stream = new MemoryStream(data);
            using var binr = new BinaryReader(stream, Encoding.ASCII);

            if (Encoding.ASCII.GetString(binr.ReadBytes(3)) != "MAP")
            {
                throw new Exception("MAP file is corrupted. Cant read header.");
            }

            var version = new string(binr.ReadChars(5));

            var name = Utils.TrimFromZero(new string(binr.ReadChars(64)));
            var width = binr.ReadByte();
            var height = binr.ReadByte();

            this.map = BuildEmptyMap(width, height);

            var layers = new Dictionary<string, List<TmxDataTile>>{
                {"Map", new List<TmxDataTile>()},
                {"Roads", new List<TmxDataTile>()},
                {"Objects", new List<TmxDataTile>()},
            };
            var objects = new Dictionary<string, List<TmxObject>>{
                {"Roads", new List<TmxObject>()},
                {"Objects", new List<TmxObject>()},
            };
            var unknowns = new Dictionary<string, List<uint>>{
                {"Map", new List<uint>()},
                {"Roads", new List<uint>()},
                {"Objects", new List<uint>()},
            };

            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                    foreach (var layer in layers.Values)
                    {
                        layer.Add(new TmxDataTile());
                    }

            for (var y = 0; y < height; y++)
            {
                var size = binr.ReadInt32();
                var innerData = Utils.UnpackRLE0(binr.ReadBytes(size), width * 0xC);

                for (var x = 0; x < width; x++)
                {
                    var ptr = x * 12;

                    var texture = innerData[ptr + 0];
                    var road = innerData[ptr + 1];
                    var player = innerData[ptr + 2];
                    var mask1 = BitConverter.ToInt32(innerData, ptr + 3);
                    var unk1 = innerData[ptr + 7];
                    var mask2 = BitConverter.ToInt32(innerData, ptr + 8);

                    var gid = (
                        texture >= 0 && texture <= 98 ? map.TileSets.First(b => b.Name == "maptile").FirstGid + texture :
                        texture >= 98 && texture <= 114 ? map.TileSets.First(b => b.Name == "rockrim").FirstGid + texture - 98 :
                        texture >= 115 && texture <= 199 ? map.TileSets.First(b => b.Name == "maptile").FirstGid + texture :
                        texture >= 200 && texture <= 201 ? map.TileSets.First(b => b.Name == $"forest{texture - 199}").FirstGid :
                        texture >= 225 && texture <= 225 ? map.TileSets.First(b => b.Name == $"tester").FirstGid :
                        texture >= 226 && texture <= 226 ? map.TileSets.First(b => b.Name == $"tester2").FirstGid :
                        texture >= 227 && texture <= 232 ? map.TileSets.First(b => b.Name == $"rock0{texture - 225}").FirstGid :
                        texture >= 234 && texture <= 250 ? map.TileSets.First(b => b.Name == $"maptile").FirstGid + texture - 119 :
                        -texture
                        );

                    if (gid < 0)
                    {
                        unknowns["Map"].Add((uint)-gid);
                    }
                    else
                    {
                        layers["Map"][x + y * width].Gid = (uint)gid;
                    }

                    if (road > 0)
                    {
                        gid = map.TileSets.First(b => b.Name == "roads").FirstGid + road - 1;

                        layers["Roads"][x + y * width].Gid = (uint)gid;
                        objects["Roads"].Add(new TmxObject
                        {
                            X = x * map.TileWidth * 1.03f / 2,
                            Y = y * map.TileHeight * 1.0125f,
                            Gid = (uint)gid,
                        });
                    }
                }
            }

            var globalArrayPointer = binr.ReadInt32();
            var arrayPointer = binr.ReadInt32();
            var arraySize = binr.ReadInt32();

            for (var i = 0; i < arraySize; i++)
            {
                if (binr.ReadByte() == 0)
                {
                    continue;
                }

                var size = binr.ReadInt32();
                var innerData = Utils.UnpackRLE0(binr.ReadBytes(size), 0x13A);
                if (BitConverter.ToInt32(innerData, 236) != 0)
                {
                    Array.Copy(binr.ReadBytes(innerData[0xF0]), 0, innerData, 236, innerData[0xF0]);
                }

                if (innerData[212] == 98)
                {
                    Array.Copy(binr.ReadBytes(32), 0, innerData, 0x3C, 32);
                }

                if (BitConverter.ToInt32(innerData, 56) != 0)
                {
                    Array.Copy(binr.ReadBytes(2), 0, innerData, 0x38, 2);
                }

                var indexbytes = BitConverter.GetBytes(i);
                Array.Copy(indexbytes, 0, innerData, 142, 4);

                var x = innerData[0xD8];
                var y = innerData[0xD9];
                var klass = innerData[0xD4];
                var state = innerData[0xEA];
                var field2 = innerData[0xF2];
                var frame = innerData[0xF9];
                var ownerPlayer = innerData[0xF7];
                var sex = innerData[0xF4];
                var isFleeing = innerData[0x106] != 0;
                var genPeriod = innerData[0xE5];
                var isGoing = innerData[0x11D] != 0;
                var goingToX = innerData[0xDD];
                var goingToY = innerData[0xDE];

                // + buzz sick
                // + aargh fire

                var gid = (
                    klass == 0 ? map.TileSets.First(b => b.Name == $"bigfir").FirstGid : // +1 winter (current_season=3)
                    klass == 1 ? map.TileSets.First(b => b.Name == $"church").FirstGid : // + l_holy01 + l_holy01_mfb night
                    klass == 2 ? map.TileSets.First(b => b.Name == $"peashut").FirstGid : // + l_peas01 night, +peasover open, +baby frame=1, +hutsmoke winter
                    klass == 5 ? map.TileSets.First(b => b.Name == $"tree02").FirstGid + frame : // - tree04 winter
                    klass == 6 ? map.TileSets.First(b => b.Name == $"tree10").FirstGid + frame : // - tree11 winter
                    klass == 7 ? map.TileSets.First(b => b.Name == $"flames").FirstGid + 100 :
                    klass == 9 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 16 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 25 ? map.TileSets.First(b => b.Name == $"crate").FirstGid : // + gasmold
                    klass == 26 ? map.TileSets.First(b => b.Name == $"probe").FirstGid + frame :
                    klass == 28 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 29 ? map.TileSets.First(b => b.Name == $"wall").FirstGid + frame :
                    klass == 33 && field2 != 0 ? map.TileSets.First(b => b.Name == $"breeder").FirstGid + frame * 5 + genPeriod :
                    klass == 33 && field2 == 0 ? map.TileSets.First(b => b.Name == $"egg").FirstGid + frame :
                    klass == 34 ? map.TileSets.First(b => b.Name == $"gas").FirstGid + 100 :
                    klass == 36 ? map.TileSets.First(b => b.Name == $"spike").FirstGid + 100 :
                    klass == 37 ? map.TileSets.First(b => b.Name == $"skulls").FirstGid + 100 :
                    klass == 38 ? map.TileSets.First(b => b.Name == $"gasmold").FirstGid + 100 :
                    klass == 39 ? map.TileSets.First(b => b.Name == $"lamp").FirstGid :
                    klass == 40 ? map.TileSets.First(b => b.Name == $"skulls").FirstGid + 100 :
                    klass == 41 ? map.TileSets.First(b => b.Name == $"airstrik").FirstGid :
                    klass == 42 ? map.TileSets.First(b => b.Name == $"cloud").FirstGid : // + lgtning + blkcloud 
                    klass == 43 ? map.TileSets.First(b => b.Name == $"cloud").FirstGid : // + rain + blkcloud 
                    klass == 44 ? map.TileSets.First(b => b.Name == $"equake").FirstGid :
                    klass == 47 ? map.TileSets.First(b => b.Name == $"well").FirstGid : // +l_well01 night, +wellover unk
                    klass == 48 ? map.TileSets.First(b => b.Name == $"logs").FirstGid :
                    klass == 49 ? map.TileSets.First(b => b.Name == $"stump").FirstGid :
                    klass == 50 ? map.TileSets.First(b => b.Name == $"flag").FirstGid + 100 :
                    klass == 51 ? map.TileSets.First(b => b.Name == $"farm").FirstGid : // + l_farm01 night, +farmover unk, +feed state=1, +cocker state=2, +cowshed sate=3, +pigsy state=4
                    klass == 53 ? map.TileSets.First(b => b.Name == $"bakery").FirstGid : // + l_bake01 night, +bakeover unk, +bakeanim
                    klass == 54 ? map.TileSets.First(b => b.Name == $"brewery").FirstGid : // + l_brew01 night, +brewover unk, +brewsmal frame >= 3, +brewbig frame < 3
                    klass == 55 ? map.TileSets.First(b => b.Name == $"rocks").FirstGid :
                    klass == 56 ? map.TileSets.First(b => b.Name == $"flames").FirstGid : // + fireball
                    klass == 57 ? map.TileSets.First(b => b.Name == $"cross").FirstGid :
                    klass == 58 && field2 == 0x1bu && state == 1 ? map.TileSets.First(b => b.Name == $"d_male").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 2 ? map.TileSets.First(b => b.Name == $"d_fema").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 3 ? map.TileSets.First(b => b.Name == $"d_prie").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 4 ? map.TileSets.First(b => b.Name == $"d_buil").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 5 ? map.TileSets.First(b => b.Name == $"d_taxm").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 6 ? map.TileSets.First(b => b.Name == $"d_pike").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 7 ? map.TileSets.First(b => b.Name == $"d_foot").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 8 ? map.TileSets.First(b => b.Name == $"d_knig").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 9 ? map.TileSets.First(b => b.Name == $"d_wiza").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 11 ? map.TileSets.First(b => b.Name == $"d_oldm").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 12 ? map.TileSets.First(b => b.Name == $"d_oldw").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 13 ? map.TileSets.First(b => b.Name == $"d_kidm").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 14 ? map.TileSets.First(b => b.Name == $"d_kidf").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 16 ? map.TileSets.First(b => b.Name == $"d_cava").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 19 ? map.TileSets.First(b => b.Name == $"d_arch").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 20 ? map.TileSets.First(b => b.Name == $"d_jest").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 32 ? map.TileSets.First(b => b.Name == $"d_groom").FirstGid + frame :
                    klass == 58 && field2 == 0x1bu && state == 33 ? map.TileSets.First(b => b.Name == $"d_bride").FirstGid + frame :
                    klass == 58 && field2 == 0x1Cu ? map.TileSets.First(b => b.Name == $"murd_wst").FirstGid + frame :
                    klass == 58 && field2 == 0x1Du ? map.TileSets.First(b => b.Name == $"zombdead").FirstGid + frame :
                    klass == 58 && field2 == 0x1Eu ? map.TileSets.First(b => b.Name == $"cowchopm").FirstGid + frame :
                    klass == 58 && field2 == 0x1Fu ? map.TileSets.First(b => b.Name == $"cowplode").FirstGid + frame :
                    klass == 58 && field2 == 0x21u ? map.TileSets.First(b => b.Name == $"pikedie").FirstGid + frame :
                    klass == 58 && field2 == 0x22u ? map.TileSets.First(b => b.Name == $"footdie").FirstGid + frame :
                    klass == 58 && field2 == 0x23u ? map.TileSets.First(b => b.Name == $"flagdie").FirstGid + frame :
                    klass == 58 && field2 == 0x24u ? map.TileSets.First(b => b.Name == $"wizadie").FirstGid + frame :
                    klass == 58 && field2 == 0x25u ? map.TileSets.First(b => b.Name == $"skeleton").FirstGid + frame :
                    klass == 58 && field2 == 0x62u && state == 23 ? map.TileSets.First(b => b.Name == $"wolfdead").FirstGid + frame :
                    klass == 58 && field2 == 0x62u && state != 23 ? map.TileSets.First(b => b.Name == $"darkdead").FirstGid + frame :
                    klass == 58 && field2 == 0x63u && state == 24 ? map.TileSets.First(b => b.Name == $"waspdead").FirstGid + frame :
                    klass == 58 && field2 == 0x63u && state != 24 ? map.TileSets.First(b => b.Name == $"bloodead").FirstGid + frame :
                    klass == 58 && field2 == 0x68u ? map.TileSets.First(b => b.Name == $"deadbat").FirstGid + frame :
                    klass == 58 && field2 == 0x69u ? map.TileSets.First(b => b.Name == $"giandead").FirstGid + frame :
                    klass == 58 && field2 == 0x71u ? map.TileSets.First(b => b.Name == $"ogredead").FirstGid + frame :
                    klass == 58 && field2 == 0x72u ? map.TileSets.First(b => b.Name == $"vampdead").FirstGid + frame :
                    klass == 58 && field2 == 0x76u ? map.TileSets.First(b => b.Name == $"cowkill").FirstGid + frame :
                    klass == 58 && field2 == 0x77u ? map.TileSets.First(b => b.Name == $"cowdie").FirstGid + frame :
                    klass == 60 ? map.TileSets.First(b => b.Name == $"bench").FirstGid :
                    klass == 61 ? map.TileSets.First(b => b.Name == $"oilslick").FirstGid :
                    klass == 62 ? map.TileSets.First(b => b.Name == $"glue").FirstGid :
                    klass == 63 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 64 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 66 ? map.TileSets.First(b => b.Name == $"th_nodo").FirstGid : // +l_tax01 night, +ticktock day, +taxover unk
                    klass == 67 ? map.TileSets.First(b => b.Name == $"firebomb").FirstGid :
                    klass == 68 ? map.TileSets.First(b => b.Name == $"shrapnel").FirstGid :
                    klass == 69 ? map.TileSets.First(b => b.Name == $"toolshed").FirstGid : // +sign always, +l_work01 night, +workover unk
                    klass == 70 && field2 >= 21 && field2 <= 22 ? map.TileSets.First(b => b.Name == $"fired").FirstGid + field2 - 19 :
                    klass == 70 ? map.TileSets.First(b => b.Name == $"armed").FirstGid :
                    klass == 71 ? map.TileSets.First(b => b.Name == $"cbolt").FirstGid + genPeriod >> 6 :
                    klass == 72 ? map.TileSets.First(b => b.Name == $"c_target").FirstGid :
                    klass == 73 ? map.TileSets.First(b => b.Name == $"chapel").FirstGid :
                    klass == 74 ? map.TileSets.First(b => b.Name == $"bridge").FirstGid + frame :
                    klass == 75 ? map.TileSets.First(b => b.Name == $"treehous").FirstGid :
                    klass == 76 ? map.TileSets.First(b => b.Name == $"barrack").FirstGid :
                    klass == 77 ? map.TileSets.First(b => b.Name == $"miniflag").FirstGid :
                    klass == 78 ? map.TileSets.First(b => b.Name == $"crops").FirstGid :
                    klass == 79 ? map.TileSets.First(b => b.Name == $"appltree").FirstGid :
                    klass == 80 ? map.TileSets.First(b => b.Name == $"thumper").FirstGid :
                    klass == 81 ? map.TileSets.First(b => b.Name == $"towreal").FirstGid : // + towout [genPeriod]
                    klass == 82 ? map.TileSets.First(b => b.Name == $"snicket").FirstGid :
                    klass == 83 ? map.TileSets.First(b => b.Name == $"poison").FirstGid + state :
                    klass == 84 ? map.TileSets.First(b => b.Name == $"worm").FirstGid :
                    klass == 85 ? map.TileSets.First(b => b.Name == $"turf").FirstGid :
                    klass == 86 ? map.TileSets.First(b => b.Name == $"summon").FirstGid : // + l_wiz01 night
                    klass == 87 ? map.TileSets.First(b => b.Name == $"pile01").FirstGid :
                    klass == 88 ? map.TileSets.First(b => b.Name == $"pile02").FirstGid :
                    klass == 89 ? map.TileSets.First(b => b.Name == $"pile03").FirstGid :
                    klass == 90 ? map.TileSets.First(b => b.Name == $"pile04").FirstGid :
                    klass == 91 ? map.TileSets.First(b => b.Name == $"bombman").FirstGid :
                    klass == 92 && field2 == 0x30u ? map.TileSets.First(b => b.Name == $"shaker").FirstGid : // + bushk_ml/bushk_fl/bushk_pr/bushk_rp sex
                    klass == 92 && field2 == 0x31u ? map.TileSets.First(b => b.Name == $"shaker").FirstGid : //
                    klass == 92 ? map.TileSets.First(b => b.Name == $"bushman").FirstGid : // + busheyes always, + bushk_ml
                    klass == 93 ? map.TileSets.First(b => b.Name == $"flamedev").FirstGid : //+ flamepit
                    klass == 94 ? map.TileSets.First(b => b.Name == $"appsprig").FirstGid :
                    klass == 95 ? map.TileSets.First(b => b.Name == $"chickegg").FirstGid :
                    klass == 96 ? map.TileSets.First(b => b.Name == $"cowborn").FirstGid :
                    klass == 97 ? map.TileSets.First(b => b.Name == $"wheatovr").FirstGid :
                    klass == 98 ? map.TileSets.First(b => b.Name == $"owner").FirstGid : // + flag
                    klass == 99 ? map.TileSets.First(b => b.Name == $"pile05").FirstGid :
                    // +buzz disease, +aargh fire, +lantern night, 
                    klass == 103 && field2 == 0 && sex == 4 && !isGoing ? map.TileSets.First(b => b.Name == $"rp_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 4 && isGoing ? map.TileSets.First(b => b.Name == $"reparman").FirstGid :
                    klass == 103 && field2 == 0 && sex == 5 && !isGoing ? map.TileSets.First(b => b.Name == $"tx_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 5 && isGoing ? map.TileSets.First(b => b.Name == $"taxman").FirstGid :
                    klass == 103 && field2 == 0 && sex == 17 ? map.TileSets.First(b => b.Name == $"bombman").FirstGid :
                    klass == 103 && field2 == 0 && sex == 3 && isFleeing ? map.TileSets.First(b => b.Name == $"fleeprie").FirstGid :
                    klass == 103 && field2 == 0 && sex == 3 && !isGoing ? map.TileSets.First(b => b.Name == $"pr_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 3 && isGoing ? map.TileSets.First(b => b.Name == $"priest").FirstGid :
                    klass == 103 && field2 == 0 && sex == 2 && isFleeing ? map.TileSets.First(b => b.Name == $"fleefema").FirstGid :
                    klass == 103 && field2 == 0 && sex == 2 && !isGoing ? map.TileSets.First(b => b.Name == $"f_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 2 && isGoing ? map.TileSets.First(b => b.Name == $"woman").FirstGid : // ~sackfema data8[0]=6, ~barlfema data8[0]=8, ~f_milk data[8]=0x13u
                    klass == 103 && field2 == 0 && sex == 1 && isFleeing ? map.TileSets.First(b => b.Name == $"fleemale").FirstGid :
                    klass == 103 && field2 == 0 && sex == 1 && !isGoing ? map.TileSets.First(b => b.Name == $"m_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 1 && isGoing ? map.TileSets.First(b => b.Name == $"villager").FirstGid : // ~sackmale data8[0]=6, m_barrel data8[0]=8
                    klass == 103 && field2 == 0 && sex == 32 ? map.TileSets.First(b => b.Name == $"m_weddin").FirstGid :
                    klass == 103 && field2 == 0 && sex == 33 ? map.TileSets.First(b => b.Name == $"f_weddin").FirstGid :
                    klass == 103 && field2 == 0 && sex == 11 ? map.TileSets.First(b => b.Name == $"elderm").FirstGid :
                    klass == 103 && field2 == 0 && sex == 12 ? map.TileSets.First(b => b.Name == $"elderf").FirstGid :
                    klass == 103 && field2 == 0 && sex == 13 && !isGoing ? map.TileSets.First(b => b.Name == $"boystill").FirstGid :
                    klass == 103 && field2 == 0 && sex == 13 && isGoing ? map.TileSets.First(b => b.Name == $"kidm").FirstGid :
                    klass == 103 && field2 == 0 && sex == 14 && !isGoing ? map.TileSets.First(b => b.Name == $"girlstil").FirstGid :
                    klass == 103 && field2 == 0 && sex == 14 && isGoing ? map.TileSets.First(b => b.Name == $"kidf").FirstGid :
                    klass == 103 && field2 == 0 && sex == 7 && !isGoing ? map.TileSets.First(b => b.Name == $"ft_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 7 && isGoing ? map.TileSets.First(b => b.Name == $"footwalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 8 && !isGoing ? map.TileSets.First(b => b.Name == $"kn_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 8 && isGoing ? map.TileSets.First(b => b.Name == $"knigwalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 19 && !isGoing ? map.TileSets.First(b => b.Name == $"arc_stil").FirstGid :
                    klass == 103 && field2 == 0 && sex == 19 && isGoing ? map.TileSets.First(b => b.Name == $"archer").FirstGid :
                    klass == 103 && field2 == 0 && sex == 20 && !isGoing ? map.TileSets.First(b => b.Name == $"minjuggl").FirstGid :
                    klass == 103 && field2 == 0 && sex == 20 && isGoing ? map.TileSets.First(b => b.Name == $"minstrel").FirstGid :
                    klass == 103 && field2 == 0 && sex == 21 && !isGoing ? map.TileSets.First(b => b.Name == $"mk_stil").FirstGid + 100 :
                    klass == 103 && field2 == 0 && sex == 21 && isGoing ? map.TileSets.First(b => b.Name == $"monkwalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 9 && !isGoing ? map.TileSets.First(b => b.Name == $"wz_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 9 && isGoing ? map.TileSets.First(b => b.Name == $"wizwalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 6 && !isGoing ? map.TileSets.First(b => b.Name == $"pk_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 6 && isGoing ? map.TileSets.First(b => b.Name == $"pikewalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 10 && !isGoing ? map.TileSets.First(b => b.Name == $"fg_still").FirstGid :
                    klass == 103 && field2 == 0 && sex == 10 && isGoing ? map.TileSets.First(b => b.Name == $"flagwalk").FirstGid :
                    klass == 103 && field2 == 0 && sex == 16 ? map.TileSets.First(b => b.Name == $"cavalier").FirstGid :
                    klass == 103 && field2 == 3 && sex == 1 ? map.TileSets.First(b => b.Name == $"cowchopm").FirstGid :
                    klass == 103 && field2 == 3 && sex == 2 ? map.TileSets.First(b => b.Name == $"cowchopf").FirstGid :
                    klass == 103 && field2 == 4 && sex == 1 ? map.TileSets.First(b => b.Name == $"m_chkill").FirstGid :
                    klass == 103 && field2 == 4 && sex == 2 ? map.TileSets.First(b => b.Name == $"f_chkill").FirstGid :
                    klass == 103 && field2 == 5 && sex == 1 ? map.TileSets.First(b => b.Name == $"m_shaker").FirstGid :
                    klass == 103 && field2 == 5 && sex == 2 ? map.TileSets.First(b => b.Name == $"f_shaker").FirstGid :
                    klass == 103 && field2 == 6 && sex == 1 ? map.TileSets.First(b => b.Name == $"mharvest").FirstGid :
                    klass == 103 && field2 == 6 && sex == 2 ? map.TileSets.First(b => b.Name == $"fharvest").FirstGid :
                    klass == 103 && field2 == 7 && sex == 1 ? map.TileSets.First(b => b.Name == $"mthrow").FirstGid :
                    klass == 103 && field2 == 7 && sex == 2 ? map.TileSets.First(b => b.Name == $"fthrow").FirstGid :
                    klass == 103 && field2 == 7 && sex == 6 ? map.TileSets.First(b => b.Name == $"pikeatta").FirstGid :
                    klass == 103 && field2 == 7 && sex == 7 ? map.TileSets.First(b => b.Name == $"footatta").FirstGid :
                    klass == 103 && field2 == 7 && sex == 8 ? map.TileSets.First(b => b.Name == $"knigatta").FirstGid :
                    klass == 103 && field2 == 7 && sex == 9 ? map.TileSets.First(b => b.Name == $"wizaim").FirstGid :
                    klass == 103 && field2 == 7 && sex == 16 ? map.TileSets.First(b => b.Name == $"cavatta").FirstGid :
                    klass == 103 && field2 == 7 && sex == 19 ? map.TileSets.First(b => b.Name == $"archshoo").FirstGid :
                    klass == 103 && field2 == 9 && sex == 1 ? map.TileSets.First(b => b.Name == $"vpick").FirstGid :
                    klass == 103 && field2 == 9 && sex != 1 ? map.TileSets.First(b => b.Name == $"pick").FirstGid :
                    klass == 103 && field2 == 10 && sex == 1 ? map.TileSets.First(b => b.Name == $"vsaw").FirstGid :
                    klass == 103 && field2 == 10 && sex != 1 ? map.TileSets.First(b => b.Name == $"saw").FirstGid :
                    klass == 103 && field2 == 11 && sex == 4 ? map.TileSets.First(b => b.Name == $"hammer").FirstGid :
                    klass == 103 && field2 == 11 && sex != 4 ? map.TileSets.First(b => b.Name == $"manham").FirstGid :
                    klass == 103 && field2 == 12 && sex == 1 ? map.TileSets.First(b => b.Name == $"m_chop").FirstGid :
                    klass == 103 && field2 == 12 && sex != 1 ? map.TileSets.First(b => b.Name == $"f_chop").FirstGid :
                    klass == 103 && field2 == 13 ? map.TileSets.First(b => b.Name == $"pri_heal").FirstGid : // +healovr always, +lantern night, +flag data8[0]=15
                    klass == 103 && field2 == 14 ? map.TileSets.First(b => b.Name == $"wizcast").FirstGid :
                    klass == 103 && field2 == 93 ? map.TileSets.First(b => b.Name == $"milking").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0xfu ? map.TileSets.First(b => b.Name == $"catfire").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0xfu ? map.TileSets.First(b => b.Name == $"catapult").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x12u ? map.TileSets.First(b => b.Name == $"zombatta").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x12u ? map.TileSets.First(b => b.Name == $"zombie").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x16u ? map.TileSets.First(b => b.Name == $"crossbow").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x16u ? map.TileSets.First(b => b.Name == $"crossbow").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x17u ? map.TileSets.First(b => b.Name == $"wolfatta").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x17u && isGoing ? map.TileSets.First(b => b.Name == $"wolf").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x17u && !isGoing ? map.TileSets.First(b => b.Name == $"wolfstil").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x18u ? map.TileSets.First(b => b.Name == $"waspat").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x18u ? map.TileSets.First(b => b.Name == $"wasp").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x19u ? map.TileSets.First(b => b.Name == $"batatta").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x19u ? map.TileSets.First(b => b.Name == $"bat").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Au ? map.TileSets.First(b => b.Name == $"fiend").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Au ? map.TileSets.First(b => b.Name == $"fiend").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Bu ? map.TileSets.First(b => b.Name == $"gianatt").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Bu && isGoing ? map.TileSets.First(b => b.Name == $"giant").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Bu && !isGoing ? map.TileSets.First(b => b.Name == $"gianstil").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Cu ? map.TileSets.First(b => b.Name == $"bldsting").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Cu ? map.TileSets.First(b => b.Name == $"bloodwsp").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Du ? map.TileSets.First(b => b.Name == $"dw_attak").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Du && isGoing ? map.TileSets.First(b => b.Name == $"dw_walk").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Du && !isGoing ? map.TileSets.First(b => b.Name == $"dw_still").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Eu ? map.TileSets.First(b => b.Name == $"ogreatta").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Eu && isGoing ? map.TileSets.First(b => b.Name == $"ogrewalk").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Eu && !isGoing ? map.TileSets.First(b => b.Name == $"ogrestil").FirstGid :
                    klass == 104 && field2 == 7 && sex == 0x1Fu ? map.TileSets.First(b => b.Name == $"vampbat").FirstGid :
                    klass == 104 && field2 != 7 && sex == 0x1Fu ? map.TileSets.First(b => b.Name == $"vampstil").FirstGid :
                    klass == 108 ? map.TileSets.First(b => b.Name == $"zombie").FirstGid :
                    klass == 110 ? map.TileSets.First(b => b.Name == $"cowwalk").FirstGid :
                    klass == 111 ? map.TileSets.First(b => b.Name == $"chikfeld").FirstGid : //+chicken, chickegg field2==92
                                                                                             // klass == 113 ? map.TileSets.First(b => b.Name == $"peashut").FirstGid : // vpick?
                    klass == 114 ? map.TileSets.First(b => b.Name == $"pick").FirstGid + state :
                    klass == 115 ? map.TileSets.First(b => b.Name == $"saw").FirstGid + state :
                    // klass == 116 ? map.TileSets.First(b => b.Name == $"saw").FirstGid : // vsaw?
                    klass == 117 ? map.TileSets.First(b => b.Name == $"bakery").FirstGid :
                    klass == 118 ? map.TileSets.First(b => b.Name == $"well").FirstGid :
                    klass == 119 ? map.TileSets.First(b => b.Name == $"farm").FirstGid :
                    klass == 122 ? map.TileSets.First(b => b.Name == $"wall").FirstGid :
                    klass == 123 ? map.TileSets.First(b => b.Name == $"toolshed").FirstGid :
                    klass == 124 ? map.TileSets.First(b => b.Name == $"brewery").FirstGid :
                    klass == 126 ? map.TileSets.First(b => b.Name == $"th_nodo").FirstGid :
                    klass == 127 ? map.TileSets.First(b => b.Name == $"church").FirstGid :
                    klass == 128 ? map.TileSets.First(b => b.Name == $"armed").FirstGid :
                    klass == 129 ? map.TileSets.First(b => b.Name == $"fence").FirstGid + frame : //~snwfence winter
                    klass == 130 ? map.TileSets.First(b => b.Name == $"fence").FirstGid :
                    klass == 131 ? map.TileSets.First(b => b.Name == $"appltree").FirstGid :
                    klass == 132 ? map.TileSets.First(b => b.Name == $"spirt").FirstGid :
                    klass == 134 ? map.TileSets.First(b => b.Name == $"sap_fir").FirstGid :
                    klass == 135 ? map.TileSets.First(b => b.Name == $"castle").FirstGid :
                    klass == 136 ? map.TileSets.First(b => b.Name == $"cabin").FirstGid :
                    klass == 137 ? map.TileSets.First(b => b.Name == $"cabin").FirstGid :
                    klass == 138 ? map.TileSets.First(b => b.Name == $"execblok").FirstGid :
                    klass == 140 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 141 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 142 ? map.TileSets.First(b => b.Name == $"rocks").FirstGid :
                    klass == 143 ? map.TileSets.First(b => b.Name == $"campfire").FirstGid + 100 :
                    klass == 144 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 145 ? map.TileSets.First(b => b.Name == $"fletcher").FirstGid : //+l_arch01 night, archdoor unk
                    klass == 146 ? map.TileSets.First(b => b.Name == $"fletcher").FirstGid :
                    klass == 147 ? map.TileSets.First(b => b.Name == $"shoot").FirstGid :
                    klass == 148 ? map.TileSets.First(b => b.Name == $"mace").FirstGid :
                    klass == 149 ? map.TileSets.First(b => b.Name == $"electro").FirstGid :
                    klass == 150 ? map.TileSets.First(b => b.Name == $"tent").FirstGid :
                    klass == 151 ? map.TileSets.First(b => b.Name == $"tent").FirstGid :
                    klass == 152 ? map.TileSets.First(b => b.Name == $"keep").FirstGid : //+keepdoor unk
                    klass == 153 ? map.TileSets.First(b => b.Name == $"keep").FirstGid :
                    klass == 154 ? map.TileSets.First(b => b.Name == $"jail").FirstGid : //+jaildoor unk
                    klass == 155 ? map.TileSets.First(b => b.Name == $"jail").FirstGid :
                    klass == 156 ? map.TileSets.First(b => b.Name == $"acidbolt").FirstGid :
                    klass == 157 ? map.TileSets.First(b => b.Name == $"stables").FirstGid : //+stabdoor unk
                    klass == 158 ? map.TileSets.First(b => b.Name == $"stables").FirstGid :
                    klass == 159 ? map.TileSets.First(b => b.Name == $"gob_silv").FirstGid + 100 :
                    klass == 160 && field2 == 84 ? map.TileSets.First(b => b.Name == $"pot_red").FirstGid + 100 :
                    klass == 160 && field2 == 85 ? map.TileSets.First(b => b.Name == $"pot_gren").FirstGid + 100 :
                    klass == 160 && field2 == 86 ? map.TileSets.First(b => b.Name == $"pot_blue").FirstGid + 100 :
                    klass == 161 ? map.TileSets.First(b => b.Name == $"goldcoin").FirstGid :
                    klass == 162 ? map.TileSets.First(b => b.Name == $"pagnbulk").FirstGid :
                    klass == 163 ? map.TileSets.First(b => b.Name == $"paganrok").FirstGid :
                    klass == 164 ? map.TileSets.First(b => b.Name == $"sword").FirstGid :
                    klass == 166 ? map.TileSets.First(b => b.Name == $"catapult").FirstGid :
                    klass == 169 ? map.TileSets.First(b => b.Name == $"gate").FirstGid + state + 4 * frame :
                    klass == 170 ? map.TileSets.First(b => b.Name == $"gate").FirstGid :
                    klass == 171 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 172 ? map.TileSets.First(b => b.Name == $"gob_gold").FirstGid + 100 :
                    klass == 173 ? map.TileSets.First(b => b.Name == $"flag").FirstGid + 100 :
                    klass == 174 ? map.TileSets.First(b => b.Name == $"holyswrd").FirstGid + 100 :
                    klass == 175 ? map.TileSets.First(b => b.Name == $"summon").FirstGid :
                    klass == 176 ? map.TileSets.First(b => b.Name == $"tablets").FirstGid + frame : //~crate frame>0x20u
                    klass == 177 ? map.TileSets.First(b => b.Name == $"helmet").FirstGid + 100 :
                    klass == 178 ? map.TileSets.First(b => b.Name == $"magring").FirstGid + 100 :
                    klass == 179 ? map.TileSets.First(b => b.Name == $"grave").FirstGid :
                    klass == 190 ? map.TileSets.First(b => b.Name == $"grave").FirstGid :
                    klass == 191 ? map.TileSets.First(b => b.Name == $"zombtomb").FirstGid :
                    klass == 192 ? map.TileSets.First(b => b.Name == $"shield").FirstGid + 100 :
                    klass == 193 ? map.TileSets.First(b => b.Name == $"crucifix").FirstGid :
                    klass == 194 && state == 36 ? map.TileSets.First(b => b.Name == $"d_spike").FirstGid :
                    klass == 194 && state != 36 ? map.TileSets.First(b => b.Name == $"d_flame").FirstGid :
                    klass == 194 ? map.TileSets.First(b => b.Name == $"d_spike").FirstGid :
                    klass == 195 ? map.TileSets.First(b => b.Name == $"s_tport").FirstGid + 100 :
                    klass == 196 ? map.TileSets.First(b => b.Name == $"s_fireb").FirstGid :
                    klass == 197 ? map.TileSets.First(b => b.Name == $"shadow").FirstGid : // ~rockshad ~speech ~artminis ~magnify ~urotate ~hand ~capture ~torch ~ufight ~uguild ~utame ~ueat ~uleave ~unoguild ~urepair ~ufight
                    klass == 198 ? map.TileSets.First(b => b.Name == $"mkey").FirstGid + 100 :
                    klass == 199 ? map.TileSets.First(b => b.Name == $"mgate").FirstGid :
                    klass == 200 ? map.TileSets.First(b => b.Name == $"shovel").FirstGid + 100 :
                    klass == 201 ? map.TileSets.First(b => b.Name == $"rocks").FirstGid :
                    klass == 202 ? map.TileSets.First(b => b.Name == $"missmoke").FirstGid + 100 :
                    klass == 203 ? map.TileSets.First(b => b.Name == $"explode").FirstGid :
                    klass == 204 ? map.TileSets.First(b => b.Name == $"smolder").FirstGid + 100 :
                    klass == 205 ? map.TileSets.First(b => b.Name == $"rocks").FirstGid :
                    klass == 206 ? map.TileSets.First(b => b.Name == $"crate").FirstGid :
                    klass == 207 ? map.TileSets.First(b => b.Name == $"fguild").FirstGid : //+footdoor unk
                    klass == 208 ? map.TileSets.First(b => b.Name == $"fguild").FirstGid :
                    klass == 209 ? map.TileSets.First(b => b.Name == $"ogrefire").FirstGid :
                    klass == 210 ? map.TileSets.First(b => b.Name == $"lever").FirstGid :
                    klass == 211 ? map.TileSets.First(b => b.Name == $"cloak").FirstGid :
                    klass == 212 ? map.TileSets.First(b => b.Name == $"power").FirstGid + 100 :
                    klass == 213 ? map.TileSets.First(b => b.Name == $"trigger").FirstGid :
                    klass == 214 ? map.TileSets.First(b => b.Name == $"youth").FirstGid :
                    klass == 215 ? map.TileSets.First(b => b.Name == $"shrub").FirstGid :
                    klass == 216 ? map.TileSets.First(b => b.Name == $"logchair").FirstGid :
                    klass == 217 ? map.TileSets.First(b => b.Name == $"altar").FirstGid :
                    klass == 218 ? map.TileSets.First(b => b.Name == $"spooky").FirstGid :
                    klass == 219 ? map.TileSets.First(b => b.Name == $"gfence").FirstGid + frame :
                    klass == 220 ? map.TileSets.First(b => b.Name == $"special").FirstGid :
                    klass == 221 ? map.TileSets.First(b => b.Name == $"lookout").FirstGid : //+bellring | look_ov
                    klass == 222 ? map.TileSets.First(b => b.Name == $"fireball").FirstGid :
                    klass == 223 ? map.TileSets.First(b => b.Name == $"s_see").FirstGid :
                    klass == 224 ? map.TileSets.First(b => b.Name == $"s_fear").FirstGid :
                    klass == 225 ? map.TileSets.First(b => b.Name == $"lookout").FirstGid :
                    klass == 226 ? map.TileSets.First(b => b.Name == $"hive").FirstGid :
                    klass == 227 ? map.TileSets.First(b => b.Name == $"s_ffood").FirstGid :
                    klass == 228 ? map.TileSets.First(b => b.Name == $"ship").FirstGid :
                    klass == 229 ? map.TileSets.First(b => b.Name == $"rubble").FirstGid :
                    klass == 230 ? map.TileSets.First(b => b.Name == $"s_flash").FirstGid + 100 :
                    klass == 231 ? map.TileSets.First(b => b.Name == $"s_cure").FirstGid + 100 :
                    -klass
                );

                if (gid < 0)
                {
                    unknowns["Objects"].Add((uint)-gid);
                }
                else
                {
                    layers["Objects"][x + y * width].Gid = gid < 0 ? 0 : (uint)gid;
                    objects["Objects"].Add(new TmxObject
                    {
                        X = x * map.TileWidth * 1.03f / 2,
                        Y = y * map.TileHeight * 1.0125f,
                        Gid = gid < 0 ? 0 : (uint)gid,
                    });
                }
            }

            foreach (var obj in objects)
            {
                map.ObjectGroups.Add(new TmxObjectGroup
                {
                    Name = obj.Key,
                    Visible = obj.Key == "Objects",
                    Objects = obj.Value.OrderBy(a => a.X + a.Y).ToList()
                });
            }

            foreach (var unk in unknowns)
            {
                map.Properties.Add(new TmxProperty
                {
                    Name = "Unknown" + unk.Key,
                    Value = string.Join(",", unk.Value.Distinct().OrderBy(a => a).Where(a => a < 0))
                });
            }

            foreach (var layer in layers)
            {
                map.Layers.Add(new TmxTileLayer
                {
                    Name = layer.Key,
                    Width = width,
                    Height = height,
                    Visible = layer.Key != "Objects",
                    Data = new TmxData
                    {
                        Encoding = "csv",
                        Tiles = layer.Value
                    }
                });
            }
        }

        private TmxMap BuildEmptyMap(byte width, byte height)
        {
            var map = new TmxMap
            {
                Orientation = TmxOrientation.Isometric,
                RenderOrder = TmxRenderOrder.RightDown,
                Width = width,
                Height = height,
                TileWidth = 78,
                TileHeight = 40,
                TileSets = new List<TmxTileSet>(),
                Layers = new List<TmxLayer>(),
                ObjectGroups = new List<TmxObjectGroup>(),
                Properties = new List<TmxProperty>()
            };

            map.TileSets = Program.VIDEO.result.Cast<MFBFile>().Select(a =>
                new TmxTileSet
                {
                    Name = a.FileName.Replace(".mfb", ""),
                    TileWidth = a.Width,
                    TileHeight = a.Height,
                    // TileOffset = new TmxTileOffset
                    // {
                    // X = a.Offset.X,
                    // Y = a.Offset.Y
                    // }
                }
            ).ToList();

            foreach (var animation in Animations())
            {
                var ts = map.TileSets.First(a => a.Name == animation.Key);
                ts.Tiles = new List<TmxTileSetTile>{
                    new TmxTileSetTile
                    {
                        Id = 100,
                        AnimationFrames = Enumerable.Range(0, animation.Value).Select(a => new TmxTilesetTileAnimationFrame
                        {
                            TileId = a,
                            Duration = 100
                        }).ToList()
                    }
                };
            }

            for (var i = 0; i < map.TileSets.Count; i++)
            {
                map.TileSets[i].FirstGid = (i + 1) * 1000;
                map.TileSets[i].Image = new TmxImage { Source = $"../VIDEO/{map.TileSets[i].Name}.png" };
            }
            return map;
        }
        private Dictionary<string, int> Animations()
        {
            return new Dictionary<string, int>{
                {"campfire", 8},
                {"flag", 10},
                {"flames", 10},
                {"gas", 11},
                {"gasmold", 12},
                {"gob_gold", 4},
                {"gob_silv", 4},
                {"helmet", 4},
                {"holyswrd", 4},
                {"magring", 4},
                {"mk_stil", 4},
                {"mkey", 4},
                {"pot_blue", 5},
                {"pot_gren", 5},
                {"pot_red", 5},
                {"power", 4},
                {"shield", 4},
                {"shovel", 4},
                {"spike", 8},
            };
        }
        public void Save(string path)
        {
            var fileName = $"{path}/{this.FileName.Replace(".mis", ".tmx", StringComparison.InvariantCultureIgnoreCase)}";
            using (var f = File.OpenWrite(fileName))
            {
                TiledHelper.Write(map, f);
            }
        }

    }
}