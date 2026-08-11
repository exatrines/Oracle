namespace Oracle.Services.FFLogs;

/// <summary>
/// Built-in FFLogs import allow-lists per ClassJob row id (job or base class).
/// Missing jobs default to an empty allow-list.
/// </summary>
internal static class FFLogsImportActionDefaults
{
    private static readonly Dictionary<uint, uint[]> ByClassJob = new()
    {
        [1u] = // 1 | GLA - Gladiator
        [
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            7548, // Arm's Length  Lv32
            17, // Sentinel  Lv38
            7537, // Shirk  Lv48
        ],
        [2u] = // 2 | PGL - Pugilist
        [
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            65, // Mantra  Lv42
            7546, // True North  Lv50
        ],
        [3u] = // 3 | MRD - Marauder
        [
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            40, // Thrill of Battle  Lv30
            7548, // Arm's Length  Lv32
            44, // Vengeance  Lv38
            43, // Holmgang  Lv42
            7537, // Shirk  Lv48
        ],
        [4u] = // 4 | LNC - Lancer
        [
            83, // Life Surge  Lv6
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
        ],
        [5u] = // 5 | ARC - Archer
        [
            7541, // Second Wind  Lv8
            7551, // Head Graze  Lv24
            7548, // Arm's Length  Lv32
        ],
        [6u] = // 6 | CNJ - Conjurer
        [
            120, // Cure  Lv2
            124, // Medica  Lv10
            7568, // Esuna  Lv10
            125, // Raise  Lv12
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            135, // Cure II  Lv30
            7559, // Surecast  Lv44
            133, // Medica II  Lv50
        ],
        [7u] = // 7 | THM - Thaumaturge
        [
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            157, // Manaward  Lv30
            7559, // Surecast  Lv44
        ],
        [19u] = // 19 | PLD - Paladin
        [
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            7548, // Arm's Length  Lv32
            3542, // Sheltron  Lv35
            17, // Sentinel  Lv38
            27, // Cover  Lv45
            7537, // Shirk  Lv48
            30, // Hallowed Ground  Lv50
            22, // Bulwark  Lv52
            3540, // Divine Veil  Lv56
            7382, // Intervention  Lv62
            7385, // Passage of Arms  Lv70
            25746, // Holy Sheltron  Lv82
            36920, // Guardian  Lv92
        ],
        [20u] = // 20 | MNK - Monk
        [
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            65, // Mantra  Lv42
            7546, // True North  Lv50
            7394, // Riddle of Earth  Lv64
            36944, // Earth's Reply  Lv64
        ],
        [21u] = // 21 | WAR - Warrior
        [
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            40, // Thrill of Battle  Lv30
            7548, // Arm's Length  Lv32
            44, // Vengeance  Lv38
            43, // Holmgang  Lv42
            7537, // Shirk  Lv48
            3551, // Raw Intuition  Lv56
            3552, // Equilibrium  Lv58
            7388, // Shake It Off  Lv68
            16464, // Nascent Flash  Lv76
            25751, // Bloodwhetting  Lv82
            36923, // Damnation  Lv92
        ],
        [22u] = // 22 | DRG - Dragoon
        [
            83, // Life Surge  Lv6
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
        ],
        [23u] = // 23 | BRD - Bard
        [
            7541, // Second Wind  Lv8
            7551, // Head Graze  Lv24
            7548, // Arm's Length  Lv32
            3561, // The Warden's Paean  Lv35
            7405, // Troubadour  Lv62
            7408, // Nature's Minne  Lv66
        ],
        [24u] = // 24 | WHM - White Mage
        [
            120, // Cure  Lv2
            124, // Medica  Lv10
            7568, // Esuna  Lv10
            125, // Raise  Lv12
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            135, // Cure II  Lv30
            136, // Presence of Mind  Lv30
            137, // Regen  Lv35
            131, // Cure III  Lv40
            37008, // Aetherial Shift  Lv40
            7559, // Surecast  Lv44
            133, // Medica II  Lv50
            140, // Benediction  Lv50
            3569, // Asylum  Lv52
            16531, // Afflatus Solace  Lv52
            3571, // Assize  Lv56
            7430, // Thin Air  Lv58
            3570, // Tetragrammaton  Lv60
            7432, // Divine Benison  Lv66
            7433, // Plenary Indulgence  Lv70
            16534, // Afflatus Rapture  Lv76
            16536, // Temperance  Lv80
            25861, // Aquaveil  Lv86
            25862, // Liturgy of the Bell  Lv90
            37010, // Medica III  Lv96
            37011, // Divine Caress  Lv100
        ],
        [25u] = // 25 | BLM - Black Mage
        [
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            157, // Manaward  Lv30
            7559, // Surecast  Lv44
        ],
        [26u] = // 26 | ACN - Arcanist
        [
            25799, // Radiant Aegis  Lv2
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            7559, // Surecast  Lv44
        ],
        [27u] = // 27 | SMN - Summoner
        [
            25799, // Radiant Aegis  Lv2
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            7559, // Surecast  Lv44
        ],
        [28u] = // 28 | SCH - Scholar
        [
            190, // Physick  Lv4
            7568, // Esuna  Lv10
            173, // Resurrection  Lv12
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            16537, // Whispering Dawn  Lv20
            185, // Adloquium  Lv30
            186, // Succor  Lv35
            16538, // Fey Illumination  Lv40
            7559, // Surecast  Lv44
            166, // Aetherflow  Lv45
            189, // Lustrate  Lv45
            188, // Sacred Soil  Lv50
            3583, // Indomitability  Lv52
            3585, // Deployment Tactics  Lv56
            3586, // Emergency Tactics  Lv58
            3587, // Dissipation  Lv60
            7434, // Excogitation  Lv62
            7436, // Chain Stratagem  Lv66
            7437, // Aetherpact  Lv70
            16542, // Recitation  Lv74
            16543, // Fey Blessing  Lv76
            16545, // Summon Seraph  Lv80
            16546, // Consolation  Lv80
            25867, // Protraction  Lv86
            25868, // Expedient  Lv90
            37012, // Baneful Impaction  Lv92
            37013, // Concitation  Lv96
            37014, // Seraphism  Lv100
            37015, // Manifestation  Lv100
            37016, // Accession  Lv100
            37037, // Emergency Tactics  Lv100
        ],
        [29u] = // 29 | ROG - Rogue
        [
            2241, // Shade Shift  Lv2
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
        ],
        [30u] = // 30 | NIN - Ninja
        [
            2241, // Shade Shift  Lv2
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
        ],
        [31u] = // 31 | MCH - Machinist
        [
            7541, // Second Wind  Lv8
            7551, // Head Graze  Lv24
            7548, // Arm's Length  Lv32
            16889, // Tactician  Lv56
            2887, // Dismantle  Lv62
        ],
        [32u] = // 32 | DRK - Dark Knight
        [
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            7548, // Arm's Length  Lv32
            3636, // Shadow Wall  Lv38
            3634, // Dark Mind  Lv45
            7537, // Shirk  Lv48
            3638, // Living Dead  Lv50
            16471, // Dark Missionary  Lv66
            7393, // The Blackest Night  Lv70
            25754, // Oblation  Lv82
            36927, // Shadowed Vigil  Lv92
        ],
        [33u] = // 33 | AST - Astrologian
        [
            3594, // Benefic  Lv2
            3606, // Lightspeed  Lv6
            3600, // Helios  Lv10
            7568, // Esuna  Lv10
            3603, // Ascend  Lv12
            7562, // Lucid Dreaming  Lv14
            3614, // Essential Dignity  Lv15
            7561, // Swiftcast  Lv18
            3610, // Benefic II  Lv26
            37017, // Astral Draw  Lv30
            37018, // Umbral Draw  Lv30
            37019, // Play I  Lv30
            37020, // Play II  Lv30
            37021, // Play III  Lv30
            37023, // the Balance  Lv30
            37024, // the Arrow  Lv30
            37025, // the Spire  Lv30
            37026, // the Spear  Lv30
            37027, // the Bole  Lv30
            37028, // the Ewer  Lv30
            3595, // Aspected Benefic  Lv34
            3601, // Aspected Helios  Lv40
            7559, // Surecast  Lv44
            3612, // Synastry  Lv50
            16552, // Divination  Lv50
            3613, // Collective Unconscious  Lv58
            16553, // Celestial Opposition  Lv60
            7439, // Earthly Star  Lv62
            7444, // Lord of Crowns  Lv70
            7445, // Lady of Crowns  Lv70
            37022, // Minor Arcana  Lv70
            16556, // Celestial Intersection  Lv74
            16557, // Horoscope  Lv76
            16559, // Neutral Sect  Lv80
            25873, // Exaltation  Lv86
            25874, // Macrocosmos  Lv90
            25875, // Microcosmos  Lv90
            37029, // Oracle  Lv92
            37030, // Helios Conjunction  Lv96
            37031, // Sun Sign  Lv100
        ],
        [34u] = // 34 | SAM - Samurai
        [
            7498, // Third Eye  Lv6
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
            36962, // Tengentsu  Lv82
        ],
        [35u] = // 35 | RDM - Red Mage
        [
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            7559, // Surecast  Lv44
            25857, // Magick Barrier  Lv86
        ],
        [36u] = // 36 | BLU - Blue Mage
        [
            7560, // Addle  Lv8
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            7559, // Surecast  Lv44
        ],
        [37u] = // 37 | GNB - Gunbreaker
        [
            16140, // Camouflage  Lv6
            7531, // Rampart  Lv8
            7540, // Low Blow  Lv12
            7533, // Provoke  Lv15
            7538, // Interject  Lv18
            7535, // Reprisal  Lv22
            7548, // Arm's Length  Lv32
            16148, // Nebula  Lv38
            16151, // Aurora  Lv45
            7537, // Shirk  Lv48
            16152, // Superbolide  Lv50
            16160, // Heart of Light  Lv64
            16161, // Heart of Stone  Lv68
            25758, // Heart of Corundum  Lv82
            36935, // Great Nebula  Lv92
        ],
        [38u] = // 38 | DNC - Dancer
        [
            7541, // Second Wind  Lv8
            7551, // Head Graze  Lv24
            7548, // Arm's Length  Lv32
            16015, // Curing Waltz  Lv52
            16012, // Shield Samba  Lv56
            16014, // Improvisation  Lv80
            25789, // Improvised Finish  Lv80
        ],
        [39u] = // 39 | RPR - Reaper
        [
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            24404, // Arcane Crest  Lv40
            7546, // True North  Lv50
        ],
        [40u] = // 40 | SGE - Sage
        [
            24284, // Diagnosis  Lv2
            24285, // Kardia  Lv4
            7568, // Esuna  Lv10
            24286, // Prognosis  Lv10
            24287, // Egeiro  Lv12
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            24288, // Physis  Lv20
            24290, // Eukrasia  Lv30
            24291, // Eukrasian Diagnosis  Lv30
            24292, // Eukrasian Prognosis  Lv30
            24294, // Soteria  Lv35
            24295, // Icarus  Lv40
            7559, // Surecast  Lv44
            24296, // Druochole  Lv45
            24298, // Kerachole  Lv50
            24299, // Ixochole  Lv52
            24300, // Zoe  Lv56
            24301, // Pepsis  Lv58
            24302, // Physis II  Lv60
            24303, // Taurochole  Lv62
            24305, // Haima  Lv70
            24309, // Rhizomata  Lv74
            24310, // Holos  Lv76
            24311, // Panhaima  Lv80
            24317, // Krasis  Lv86
            24318, // Pneuma  Lv90
            37034, // Eukrasian Prognosis II  Lv96
            37035, // Philosophia  Lv100
            37036, // Eudaimonia  Lv100
        ],
        [41u] = // 41 | VPR - Viper
        [
            7541, // Second Wind  Lv8
            7542, // Bloodbath  Lv12
            7549, // Feint  Lv22
            7548, // Arm's Length  Lv32
            7546, // True North  Lv50
        ],
        [42u] = // 42 | PCT - Pictomancer
        [
            7560, // Addle  Lv8
            34685, // Tempera Coat  Lv10
            7562, // Lucid Dreaming  Lv14
            7561, // Swiftcast  Lv18
            7559, // Surecast  Lv44
            34686, // Tempera Grassa  Lv88
        ],
    };

    public static HashSet<uint> Get(uint classJobId)
    {
        if (classJobId == 0)
            return [];

        return ByClassJob.TryGetValue(classJobId, out var ids)
            ? ids.Where(id => id != 0).ToHashSet()
            : [];
    }
}
