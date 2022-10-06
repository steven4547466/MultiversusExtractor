using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace MultiversusExtractor
{
    internal class MultiversusExtractor
    {
        // Change to your path or provide in args.
        static string PATH_TO_PAKS = "";

        static List<string> SkipAdditional = new List<string>()
        {
            "Damage",
            "BaseKnockBack",
            "KnockBackScaling",
            "KnockBackDirection",
            "HitPauseTime",
            "HitOnlyOneFighter",
            "AppliedBuffs",
            "ApplyBuffSelf?",
            "ApplyBuffHit?",
            "ApplyBuffAlly?",
            "ApplyBuffEnemy?",
            "RequireHitToApplyBuffToSelf",
            "StackCount",
            "BuffDuration",
            "MaxBounces",
            "Max Bounce Speed",
            "ItemIdleLife",
            "Durability",
            "IsThrown",
            "DestroyOnExitedArena?",
            "DestroyedOnHit",
            "OnHitBounce"
        };

        static void Main(string[] args)
        {
            StringBuilder toWrite = new StringBuilder("Name,Total Frames,Frame Start,Frame End,Damage,Base Knockback,Knockback Scaling,Pause Frames,Armor Pierce,Reflects Projectiles,Knockback Angle,Additional Properties\n");
            StringBuilder toWriteProjectile = new StringBuilder("Name,Damage,HP,Base Knockback,Knockback Scaling,Knockback Angle,Pause Frames,Single Hit,Max Bounces,Max Bounce Speed,Idle Life,Durability,Is Thrown,Destroyed On Arena Exit,Destroyed On Hit,On Hit Bounce,Status Effects,Additional Properties\n");
            
            var provider = new DefaultFileProvider(args.Length > 0 ? args[0] : PATH_TO_PAKS, SearchOption.TopDirectoryOnly);
            provider.Initialize();
            provider.SubmitKey(new(0, 0, 0, 0), new("0x419DFFC484F1CED86842DD4E6DD914F02E3E119725F556C4B9AA44432021A9AC"));
            var d = provider.Files.Where(f => new Regex("MultiVersus/Content/Panda_Main/Characters/.*/Animations?.*Montage.uasset").IsMatch(f.Key)).ToDictionary(i => i.Key, i => i.Value);
            Console.WriteLine("Starting");
            foreach (var kvp in d)
            {
                string filePath = kvp.Key;
                string[] split = filePath.Split('/');
                GameFile gameFile = kvp.Value;
                var obj = provider.LoadObjectExports(filePath);
                bool hadOne = false;
                List<StringBuilder> writing = new();
                List<string> names = new List<string>();

                bool hadOneProjectile = false;
                List<StringBuilder> writingProjectile = new();
                List<string> projectileNames = new List<string>();
                foreach (var o in obj)
                {
                    if (o.ExportType == "AnimMontage")
                    {
                        var notifies = o.Properties.Find(p => p.Name.Text == "Notifies");

                        if (notifies != null && notifies.Tag != null)
                        {
                            var x = notifies.Tag.GetValue(typeof(UScriptArray)) as UScriptArray;
                            int hitboxes = 0;
                            foreach (var prop in x.Properties)
                            {
                                var scriptStruct = prop.GetValue(typeof(FStructFallback)) as FStructFallback;

                                if (scriptStruct != null)
                                {
                                    var n = scriptStruct.Get<FName>("NotifyName").Text;
                                    if (n == "AnimNotify_Hitframes" || n.Contains("Hitframe_C"))
                                    {
                                        hadOne = true;
                                        var numFrames = -1;
                                        try
                                        {
                                            var data = provider.LoadObjectExports(filePath.Substring(0, filePath.LastIndexOf('_')) + ".uasset");
                                            foreach (var doc in data)
                                            {
                                                doc.TryGetValue(out numFrames, "NumFrames");
                                            }
                                        }
                                        catch (Exception ex) { }

                                        var notifyClass = scriptStruct.GetOrDefault<UObject>("NotifyStateClass");
                                        if (notifyClass != null)
                                        {
                                            var notifyStateName = notifyClass.Name;
                                            names.Add(notifyStateName);
                                            var start = Math.Round(scriptStruct.Get<float>("LinkValue") * 60f);
                                            var endLink = scriptStruct.Get<FStructFallback>("EndLink");
                                            if (endLink != null)
                                            {
                                                var end = Math.Round(endLink.Get<float>("LinkValue") * 60f);
                                                writing.Add(new(split[split.Length - 1].Replace("_Montage.uasset", string.Empty)
                                                    .Replace("c015", "Taz", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c017", "IronGiant", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c016", "LeBronJames", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c019", "Morty", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("C023A", "Gizmo", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("Creature", "Reindog", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c020", "Rick", StringComparison.OrdinalIgnoreCase)

                                                + " Hitbox " + hitboxes + "," + numFrames + "," + start + "," + end));
                                            }
                                        }
                                        hitboxes++;
                                    }
                                    else if (n.Contains("Spawner_C"))
                                    {
                                        var notifyClass = scriptStruct.GetOrDefault<UObject>("NotifyStateClass");
                                        if (notifyClass != null)
                                        {
                                            var notifyStateName = notifyClass.Name;
                                            projectileNames.Add(notifyStateName);
                                            writingProjectile.Add(new());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                for (int i = 0; i < names.Count; i++)
                {
                    var name = names[i];
                    var data = obj.First(x => x.Name == name);
                    var damage = data.GetOrDefault("Damage", 0f);
                    var baseKnockback = data.GetOrDefault("BaseKnockBack", 0f);
                    var knockbackScaling = data.GetOrDefault("KnockBackScaling", 1.0f);
                    var hitPauseFrames = Math.Round(data.GetOrDefault("HitPauseTime", 0f) * 60f);
                    var armorPierce = data.GetOrDefault("ArmorPierce", false);
                    var knockbackDirectionStruct = data.GetOrDefault<UScriptStruct>("KnockBackDirection") != null ? (FVector2D)data.Get<UScriptStruct>("KnockBackDirection").StructType : new FVector2D(0, 0);
                    var direction = Math.Atan2(knockbackDirectionStruct.Y, knockbackDirectionStruct.X) * (180f / Math.PI);
                    var reflect = data.GetOrDefault("ReflectProjectiles", false);
                    var additionalNotes = new StringBuilder();
                    foreach (var prop in data.Properties)
                    {
                        if (prop.Name.Text == "ReflectProjectiles" || prop.Name.Text == "ArmorPierce" || prop.Name.Text == "HitPauseTime" || prop.Name.Text == "KnockBackScaling" || prop.Name.Text == "BaseKnockBack" || prop.Name.Text == "Damage")
                            continue;

                        if (prop.Tag.GenericValue.GetType() == typeof(bool) || prop.Tag.GenericValue.GetType() == typeof(float) || prop.Tag.GenericValue.GetType() == typeof(int))
                            additionalNotes.Append(prop.Name.Text + ": " + prop.Tag.GenericValue + " ");
                    }
                    writing[i].Append($",{damage},{baseKnockback},{knockbackScaling},{hitPauseFrames},{armorPierce},{reflect},{direction},{additionalNotes.ToString()}");
                }
                for (int i = 0; i < projectileNames.Count; i++)
                {
                    var name = projectileNames[i];
                    var data = obj.First(x => x.Name == name);
                    var actor = data.GetOrDefault<UObject>("ActorClass")?.GetPathName();
                    if (actor == null)
                    {
                        continue;
                    }
                    var path = actor.Substring(0, actor.LastIndexOf('.')) + ".uasset";
                    var actorData = provider.LoadObjectExports(path);
                    var actorObjectData = actorData.Where(x => x.Template != null ? x.Template.Name.Text == "Default__ItemActorParent_C" || x.Template.Name.Text == "Default__ParentProjectilePhysics_C" : false).FirstOrDefault();
                    if (actorObjectData != null)
                    {
                        hadOneProjectile = true;
                        var actorName = actorObjectData.Name.Replace("Default__", string.Empty).Replace("_C", string.Empty)
                                                    .Replace("c015", "Taz", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c017", "IronGiant", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c016", "LeBronJames", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c019", "Morty", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("C023A", "Gizmo", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("Creature", "Reindog", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c020", "Rick", StringComparison.OrdinalIgnoreCase);
                        var hitboxInfo = actorObjectData.GetOrDefault<FStructFallback>("HitBoxDamage");
                        var damage = 0f;
                        var baseKnockback = 0f;
                        var knockbackScaling = 1f;
                        var knockbackAngle = 0d;
                        var hitPauseFrames = 0d;
                        var hitOnlyOneFighter = false;
                        var hp = actorObjectData.GetOrDefault<float>("HP");
                        if (hitboxInfo != null)
                        {
                            foreach (var prop in hitboxInfo.Properties)
                            {
                                if (prop.Name.Text.StartsWith("Damage"))
                                    damage = hitboxInfo.GetOrDefault<float>(prop.Name.Text);
                                else if (prop.Name.Text.StartsWith("BaseKnockBack"))
                                    baseKnockback = hitboxInfo.GetOrDefault<float>(prop.Name.Text);
                                else if (prop.Name.Text.StartsWith("KnockBackScaling"))
                                    knockbackScaling = hitboxInfo.GetOrDefault(prop.Name.Text, 1f);
                                else if (prop.Name.Text.StartsWith("KnockBackDirection"))
                                {
                                    var knockbackDirectionStruct = hitboxInfo.GetOrDefault<UScriptStruct>(prop.Name.Text) != null ? (FVector2D)hitboxInfo.Get<UScriptStruct>(prop.Name.Text).StructType : new FVector2D(0, 0);
                                    knockbackAngle = Math.Atan2(knockbackDirectionStruct.Y, knockbackDirectionStruct.X) * (180f / Math.PI);
                                }
                                else if (prop.Name.Text.StartsWith("HitPauseTime"))
                                {
                                    var pause = hitboxInfo.GetOrDefault<float>(prop.Name.Text);
                                    if (pause != default)
                                    {
                                        hitPauseFrames = Math.Round(pause * 60f);
                                    }
                                }
                                else if (prop.Name.Text.StartsWith("BaseKnockBack"))
                                    hitOnlyOneFighter = hitboxInfo.GetOrDefault(prop.Name.Text, false);
                            }
                        }

                        var maxBounces = actorObjectData.GetOrDefault("MaxBounces", -5);
                        if (maxBounces == -5)
                            maxBounces = actorObjectData.GetOrDefault("MaxNumberofBounces", 0);
                        var maxBounceSpeed = actorObjectData.GetOrDefault("Max Bounce Speed", -1f);
                        var idleLife = actorObjectData.GetOrDefault("ItemIdleLife", -1f);
                        var durability = actorObjectData.GetOrDefault("Durability", 0f);
                        var isThrown = actorObjectData.GetOrDefault<bool>("IsThrown");
                        var destroyOnArenaExit = actorObjectData.GetOrDefault("DestroyOnExitedArena?", true);
                        var destroyedOnHit = actorObjectData.GetOrDefault("DestroyedOnHit", true);
                        var onHitBounce = actorObjectData.GetOrDefault<float>("OnHitBounce");
                        StringBuilder statusEffects = new();
                        var onHitBuffs = actorObjectData.GetOrDefault<FStructFallback>("On Hit Buffs");
                        if (onHitBuffs != null)
                        {
                            foreach (var val in onHitBuffs.Properties)
                            {
                                if (val.Name.Text.StartsWith("OnHitBuffs"))
                                {
                                    var arr = onHitBuffs.Get<UScriptArray>(val.Name.Text);
                                    foreach (var item in arr.Properties)
                                    {
                                        if (item.GenericValue != null)
                                        {
                                            var dict = (FStructFallback)((UScriptStruct)item.GenericValue).StructType;
                                            var buffPath = dict.Get<UObject>(dict.Properties.First(x => x.Name.Text.StartsWith("AppliedBuffs")).Name.Text).Outer.Name;
                                            var buffName = buffPath.Substring(buffPath.LastIndexOf('/') + 1);
                                            var applyToSelf = true;
                                            var requireHitToApplyToSelf = true;
                                            var applyBuffOnHit = false;
                                            var applyBuffAlly = true;
                                            var applyBuffEnemy = false;
                                            var stackCount = 0;
                                            var duration = -1f;
                                            foreach (var item2 in dict.Properties)
                                            {
                                                if (item2.Name.Text.StartsWith("ApplyBuffSelf?"))
                                                    applyToSelf = dict.Get<bool>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("ApplyBuffHit?"))
                                                    applyBuffOnHit = dict.Get<bool>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("ApplyBuffAlly?"))
                                                    applyBuffAlly = dict.Get<bool>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("ApplyBuffEnemy?"))
                                                    applyBuffEnemy = dict.Get<bool>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("RequireHitToApplyBuffToSelf"))
                                                    requireHitToApplyToSelf = dict.Get<bool>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("StackCount"))
                                                    stackCount = dict.Get<int>(item2.Name.Text);
                                                else if (item2.Name.Text.StartsWith("BuffDuration"))
                                                    duration = dict.Get<float>(item2.Name.Text);
                                            }
                                            statusEffects.Append($"Name: {buffName} | Apply to self: {applyToSelf} (Requires hit: {requireHitToApplyToSelf}) | Apply on hit: {applyBuffOnHit} | Apply to ally: {applyBuffAlly} | Apply to enemy: {applyBuffEnemy} | Stack count: {stackCount} | Duration: {duration} < ");
                                        }
                                    }
                                }
                            }
                        }

                        StringBuilder additionalProperties = new();
                        var props = actorObjectData.Properties.Where(p =>
                        {
                            if (p.Tag?.GenericValue == null)
                                return false;

                            if (p.Tag.GenericValue.GetType() == typeof(bool) || p.Tag.GenericValue.GetType() == typeof(float) || p.Tag.GenericValue.GetType() == typeof(int))
                            {
                                foreach (string n in SkipAdditional)
                                {
                                    if (p.Name.Text.StartsWith(n))
                                        return false;
                                }

                                return true;
                            }
                            return false;
                        });

                        foreach (var prop in props)
                        {
                            additionalProperties.Append(prop.Name.Text + ": " + prop.Tag.GenericValue + " ");
                        }
                        writingProjectile[i].Append($"{actorName},{damage},{hp},{baseKnockback},{knockbackScaling},{knockbackAngle},{hitPauseFrames},{hitOnlyOneFighter},{maxBounces},{maxBounceSpeed},{idleLife},{durability},{isThrown},{destroyOnArenaExit},{destroyedOnHit},{onHitBounce},{statusEffects.ToString()},{additionalProperties.ToString()}");
                    }

                }
                if (hadOne)
                {
                    foreach (StringBuilder sb in writing)
                    {
                        toWrite.AppendLine(sb.ToString());
                    }
                }
                if (hadOneProjectile)
                {
                    foreach (StringBuilder sb in writingProjectile)
                    {
                        var s = sb.ToString();
                        if (s.Trim().Length > 0)
                            toWriteProjectile.AppendLine(s);
                    }
                }
            }

            File.WriteAllText("hitframe-data.csv", toWrite.ToString());
            File.WriteAllText("projectile-data.csv", toWriteProjectile.ToString());


            var pawnsTemp = provider.Files.Where(f => new Regex("MultiVersus/Content/Panda_Main/Characters/.*Pawn_.*.uasset", RegexOptions.IgnoreCase).IsMatch(f.Key)).ToDictionary(i => i.Key, i => i.Value);

            var pawns = pawnsTemp.Concat(provider.Files.Where(f => new Regex("MultiVersus/Content/Panda_Main/Blueprints/Characters/.*Pawn_.*.uasset", RegexOptions.IgnoreCase).IsMatch(f.Key)).ToDictionary(i => i.Key, i => i.Value)).ToDictionary(i => i.Key, i => i.Value);

            var defaultPawn = provider.LoadObjectExports("MultiVersus/Content/Panda_Main/Blueprints/BaseFighter.uasset");

            StringBuilder sbChars = new("Character,Weight,Max Acceleration,Braking Deceleration Walking,Ground Friction,Max Walk Speed,Max Walk Speed Crouched,Max Custom Movement Speed,Min Analog Walk Speed,Jump Velocity,Max Jump Height,Max Double Jump Height,Max Wall Jump Height,Velocity From Fast Fall,Gravity Scale,Mass,Max Air Acceleration,Air Control\n");

            var defaults = new Dictionary<string, float>();

            {
                var weight = defaultPawn.FirstOrDefault(w => w.ExportType == "Comp_Actor_Damage_C").Get<float>("Weight");
                //Character,Weight,Max Acceleration,Braking Deceleration Walking,Ground Friction,Max Walk Speed,Max Walk Speed Crouched,Max Custom Movement Speed,Min Analog Walk Speed,
                //Jump Velocity,Max Jump Height,Max Double Jump Height,Max Wall Jump Height,Velocity From Fast Fall,
                //Gravity Scale,Mass,Max Air Acceleration,Air Control
                var moveComp = defaultPawn.FirstOrDefault(w => w.ExportType == "FighterMovementComponent");
                var maxAcceleration = moveComp.Get<float>("MaxAcceleration");
                var brakingDeceleration = moveComp.Get<float>("BrakingDecelerationWalking");
                var groundFriction = moveComp.Get<float>("GroundFriction");
                var maxWalkSpeed = moveComp.Get<float>("MaxWalkSpeed");
                var maxWalkSpeedCrouched = moveComp.Get<float>("MaxWalkSpeedCrouched");
                var maxCustomMovementSpeed = moveComp.Get<float>("MaxCustomMovementSpeed");
                var minAnalogWalkSpeed = moveComp.Get<float>("MinAnalogWalkSpeed");


                var mainComp = defaultPawn.FirstOrDefault(w => w.Template != null && (w.Template.Name.Text == "Default__BP_FighterCharacter_C" || w.Template.Name.Text == "Default__BaseFighter_C"));
                var jumpVelocity = mainComp.Get<float>("JumpVelocity");
                var maxJumpheight = mainComp.Get<float>("MaxJumpHeight");
                var maxDoubleJumpHeight = mainComp.Get<float>("MaxDoubleJumpHeight");
                var maxWallJumpHeight = mainComp.Get<float>("MaxWallJumpHeight");
                var velocityFromFastFall = mainComp.Get<float>("VelocityFromFastFall");

                var gravityScale = moveComp.Get<float>("GravityScale");
                var mass = moveComp.Get<float>("Mass");
                var maxAirAcceleration = moveComp.Get<float>("MaxAccelerationAir");
                var airControl = moveComp.Get<float>("AirControl");

                defaults["weight"] = weight;
                defaults["maxAcceleration"] = maxAcceleration;
                defaults["brakingDeceleration"] = brakingDeceleration;
                defaults["groundFriction"] = groundFriction;
                defaults["maxWalkSpeed"] = maxWalkSpeed;
                defaults["maxWalkSpeedCrouched"] = maxWalkSpeedCrouched;
                defaults["maxCustomMovementSpeed"] = maxCustomMovementSpeed;
                defaults["minAnalogWalkSpeed"] = minAnalogWalkSpeed;
                defaults["jumpVelocity"] = jumpVelocity;
                defaults["maxJumpHeight"] = maxJumpheight;
                defaults["maxDoubleJumpHeight"] = maxDoubleJumpHeight;
                defaults["maxWallJumpHeight"] = maxWallJumpHeight;
                defaults["velocityFromFastFall"] = velocityFromFastFall;
                defaults["gravityScale"] = gravityScale;
                defaults["mass"] = mass;
                defaults["maxAirAcceleration"] = maxAirAcceleration;
                defaults["airControl"] = airControl;

                sbChars.AppendLine($"Base Character,{weight},{maxAcceleration},{brakingDeceleration},{groundFriction},{maxWalkSpeed},{maxWalkSpeedCrouched},{maxCustomMovementSpeed},{minAnalogWalkSpeed},{jumpVelocity},{maxJumpheight},{maxDoubleJumpHeight},{maxWallJumpHeight},{velocityFromFastFall},{gravityScale},{mass},{maxAirAcceleration},{airControl}");
            }

            foreach (var kvp in pawns)
            {
                string filePath = kvp.Key;
                if (filePath.Contains("Anim"))
                    continue;
                string[] split = filePath.Split('/');
                GameFile gameFile = kvp.Value;
                var obj = provider.LoadObjectExports(filePath);

                var weight = obj.FirstOrDefault(w => w.ExportType == "Comp_Actor_Damage_C")?.GetOrDefault("Weight", defaults["weight"]) ?? defaults["weight"];
                //Character,Weight,Max Acceleration,Braking Deceleration Walking,Ground Friction,Max Walk Speed,Max Walk Speed Crouched,Max Custom Movement Speed,Min Analog Walk Speed,
                //Jump Velocity,Max Jump Height,Max Double Jump Height,Max Wall Jump Height,Velocity From Fast Fall,
                //Gravity Scale,Mass,Max Air Acceleration,Air Control
                var moveComp = obj.FirstOrDefault(w => w.ExportType == "FighterMovementComponent");
                var maxAcceleration = moveComp?.GetOrDefault("MaxAcceleration", defaults["maxAcceleration"]) ?? defaults["maxAcceleration"];
                var brakingDeceleration = moveComp?.GetOrDefault("BrakingDecelerationWalking", defaults["brakingDeceleration"]) ?? defaults["brakingDeceleration"];
                var groundFriction = moveComp?.GetOrDefault("GroundFriction", defaults["groundFriction"]) ?? defaults["groundFriction"];
                var maxWalkSpeed = moveComp?.GetOrDefault("MaxWalkSpeed", defaults["maxWalkSpeed"]) ?? defaults["maxWalkSpeed"];
                var maxWalkSpeedCrouched = moveComp?.GetOrDefault("MaxWalkSpeedCrouched", defaults["maxWalkSpeedCrouched"]) ?? defaults["maxWalkSpeedCrouched"];
                var maxCustomMovementSpeed = moveComp?.GetOrDefault("MaxCustomMovementSpeed", defaults["maxCustomMovementSpeed"]) ?? defaults["maxCustomMovementSpeed"];
                var minAnalogWalkSpeed = moveComp?.GetOrDefault("MinAnalogWalkSpeed", defaults["minAnalogWalkSpeed"]) ?? defaults["minAnalogWalkSpeed"];


                var mainComp = obj.FirstOrDefault(w => w.Template != null && (w.Template.Name.Text == "Default__BP_FighterCharacter_C" || w.Template.Name.Text == "Default__BaseFighter_C"));

                if (mainComp == null)
                {
                    Console.WriteLine("NULL COMP: " + filePath);
                    continue;
                }

                var character = mainComp.ExportType.Replace("Pawn_", string.Empty, StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c015", "Taz", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c017", "IronGiant", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c016", "LeBronJames", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c019", "Morty", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("C023A", "Gizmo", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("Creature", "Reindog", StringComparison.OrdinalIgnoreCase)
                                                    .Replace("_C", string.Empty, StringComparison.OrdinalIgnoreCase)
                                                    .Replace("c020", "Rick", StringComparison.OrdinalIgnoreCase);

                var jumpVelocity = mainComp.GetOrDefault("JumpVelocity", defaults["jumpVelocity"]);
                var maxJumpheight = mainComp.GetOrDefault("MaxJumpHeight", defaults["maxJumpHeight"]);
                var maxDoubleJumpHeight = mainComp.GetOrDefault("MaxDoubleJumpHeight", defaults["maxDoubleJumpHeight"]);
                var maxWallJumpHeight = mainComp.GetOrDefault("MaxWallJumpHeight", defaults["maxWallJumpHeight"]);
                var velocityFromFastFall = mainComp.GetOrDefault("VelocityFromFastFall", defaults["velocityFromFastFall"]);

                var gravityScale = moveComp?.GetOrDefault("GravityScale", defaults["gravityScale"]) ?? defaults["gravityScale"];
                var mass = moveComp?.GetOrDefault("Mass", defaults["mass"]) ?? defaults["mass"];
                var maxAirAcceleration = moveComp?.GetOrDefault("MaxAccelerationAir", defaults["maxAirAcceleration"]) ?? defaults["maxAirAcceleration"];
                var airControl = moveComp?.GetOrDefault("AirControl", defaults["airControl"]) ?? defaults["airControl"];

                sbChars.AppendLine($"{character},{weight},{maxAcceleration},{brakingDeceleration},{groundFriction},{maxWalkSpeed},{maxWalkSpeedCrouched},{maxCustomMovementSpeed},{minAnalogWalkSpeed},{jumpVelocity},{maxJumpheight},{maxDoubleJumpHeight},{maxWallJumpHeight},{velocityFromFastFall},{gravityScale},{mass},{maxAirAcceleration},{airControl}");
            }

            File.WriteAllText("character-data.csv", sbChars.ToString());
        }
    }
}