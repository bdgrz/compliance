import { defineApi, createClient, content, del, empty, get, json, post, put } from "@askrjs/fetch";
import type { ClientOptions } from "@askrjs/fetch";
import type { Portia013D7F6C3C0FEB6E8082C97A2D31504B222E662A6A84DD2B56600D5BF2D16139, Portia022A384CC13FCCBBBDACFD95B82E4D9EE161732C5FF61B3FF97C70AF11E4D94C, Portia03ACFAAE67D35BA82DFA61EDE278D3779AC79928770C69B153337B1F788534A3, Portia055CA1599B22C4A39EEDF56A03F53F231CEBE6075D8937F744977DD9D66E54FD, Portia069969C2489ADD7162ECE80D4C8B7385BA13F5931CCE303868141921126F6CBF, Portia097249E53E54CC03E10877C906EFC2594FFE7AAC91C87D347D57D7145C1CC24C, Portia0B6601B102C5751CB9E0454A861D244D753DE3A3F0BA4871D392E5D08B917A5F, Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D, Portia0BBEEB50D9C1BAC9B12E86679F1B76D5365AB62E7358C3F3CBF8735D3BE93FDA, Portia0F54B0A48070763F53BE45B0F025C1745F6F2EE7C0D73202A16DA9FE1E41E536, Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7, Portia0FBE6D4CEB2CC84764DF0FFF82C76BB333FB69E323061CA548534BC3DFB78B51, Portia119B1B96AE3CD15CA98EA1845F142D63F8BCAC26FF8D09C7414F2C69563FF5B0, Portia141A8449007BC3642408B5E3A41E19F30E829AC6B607080B6F410B38E370BD2E, Portia1667971800402A6979C26E522A3DC756AAF0573767E53910C942723A6B2DC3D5, Portia166CE150C5FA11C2FF07F303878BE1D989853E482321CEFA07EC5F3D792CC0DF, Portia17403E183EEEE5417F57DA6B2D792B7A88B5E631D52B05919D48C9D69FFB6C0D, Portia17488FFB4D3C927EDA1FEEAABE0836C927B726E447755DAE04EE2C5CFE567A82, Portia183272C2785AB7DAF0DDB9887649BB6524E07D26A9750A0F1422C8AD50831FB9, Portia1A72141BBDF886D72A311BA94564A77E3CFA5B0DBA1A3FD78CF78C689F1F779D, Portia1AABC072058538BE7FD1B099EC8BCE55A4F7A5AAF4C4B650FF18FB1F414E1E86, Portia1B41424DD1048776063A851A5730872FAE1FDEBEDCC12ABB187E9407CB0972F3, Portia1B51602F06141206ED19B855080CC6A10F95F1354F62A54606D6649DA73FBC07, Portia1C25C4B0018EA4C94541F8463FC978A3F4C63744AC2F9FC2D98FEE5B6F55AA8E, Portia1D599653835771B5B38C77F6C963CA25D736248D35B45B1CD6CED58C58E70B11, Portia1DAABC0EE92F473717483C90B8CF7E689206159947287755819C2AE6D3D4ECF4, Portia1E516B7E59F08223EE49BB74695B06EE1AE7FE8D6BF8E50F76EE68386F7A72DA, Portia1E5BBCD6BE4B1F969208CC6975472A2A383EBE2A1300801E1AE359E9E687516A, Portia1E60B56A95A64AF1BB8BE85BC8B2174D1CD473A879DA9B1370C87F09E86C448F, Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD, Portia1FF79BD41EDC10E67AF82C5AC8AA375E30EB20870D88CD7322218F9CD2983831, Portia223D8ED908741464C092218194160F10C7407F5339064207F7EDDFB8FC5706D2, Portia24AADB452F032CF586816A93E0006D88CF6B74A01ECC66A7DD7E222A5F991AAF, Portia2664E66364A3BD18497EAE1B5A14F44FE6851C2CFF87AA850690EA322CF1731E, Portia274B659861C11787AC6AB4C8D76C648726966FA7DA2D30804A55E91A1C4D27B2, Portia28D5187F005E5A41920CA5A644AAA3FD5806E031D8B7A74C154FFEB4E97A5BB3, Portia293AC99442386DA2E4203C76C9DCACD9B0159C4FF4F351D4C5C91AF91E5AADA5, Portia293DE426AA53AD49D4AE678B0FEDB2F4734ED0C23AA4984147FA026082080856, Portia29DA1B59E17FFC5BC5537FA36443BD20DD5A6C59FED0530C3D8E91FBB7EAB977, Portia29FC8CE420AC2814A94627199085A8D4740B2FCFF2200FDDEF4D0FCFD528B72D, Portia2AED3AF47093A366AD687BFFDD638FD60E8E83EBFD47EB5EB710621485B5041F, Portia2D5AB432C63465374C50AF5FA1724B74CECAE9F5CB78FC5BE2C2AF45228FF552, Portia2D5CCE34626A84B45EE900195D49320E848D2908BBBBF8C24AF6A2EAF51BBCBA, Portia30E355AC0C727AD6771307EDF8639387797EB7EF33A7FF597E35281049D3A67B, Portia33A914476F5916BA05D9CCA32D0BA6790B75A8EBFC139D7F65E58BAF1716FF4D, Portia36D1608FA46982FB7C0145375EB90B733846AA018C6B495E9A88F5B790185F37, Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197, Portia38AC222880FC3D76BC0113586E6EADD6FC945BF8A6AE17809DC976132058D642, Portia3A00A9545DBC606A9D844545423CA77D102CDF0448DC82A4001C72CD45E3849A, Portia3A795A94620F3B58B5F1452221FC679AB9B2B1CA5FD22BC2E89D277F4D764F4B, Portia3AA867E8899C6C43AB80B916C3F53DC57184409D52C29C8217ABCD226DD97BB5, Portia3BD7B3C292671FD9C7BC3514F31B918E92F82B060108984374D5D9FD3FAF7737, Portia3C2330B1E721B3907D876EAF59B8BADC89F6106BC2A83EBC53F96A325C8F9D64, Portia3C70E8DCFE6903F2FDCA0DF299E0A8C4BE8585E75798B4037723C1A3360251E4, Portia3D403D282DFA1E4ED98B4578E4D7850CCEC17460A271A4EEE9124D80AD091488, Portia3F6E09D9310B082917FB4E979E225D9A959D6DE8796E3B5D244A47C587B91F82, Portia3FB59D2954C0596502DAAFC4D857D1FECF2F3D8E1E967CE98B8A0A7D8B5A8D6F, Portia411B3239057A525C3562E4BE0D1A71F30A3844A0A56231069471136CDD4A37E4, Portia42415CD397320D0C15EC3283D7AB52C15BCC5C24CB26307A7BCA7CB15F1D7ABF, Portia444702261363EF785C20C78504EBD948CC2950CA076C0FEA93FBD650F83E4680, Portia445823D45DEBC21876B8F7D21A7706F96E8DB18D68A39E931F58C101C313F09E, Portia452F41728C6AFA9916F07B977123DCCF98284CE3451ABA0C4C76C709D48F8747, Portia45F55D96B4D015448EC3803E9C00616C8AA2610641128A2A4A9CCB81303CEE9D, Portia46334DBC52E6A1AB64DEA808B9B7518BD8CA5116F786F7BAAF173857E10A75C3, Portia463D989E08D87242FE062F7ABA25B4A4852CDED11C651DDB003CB1493628FBA2, Portia4745E5442B58B410B21973D635D16710CD3A17B4B309CF195296D53640E3785B, Portia474D872CD82992E0F6DAA877E95A738CD0E651D58C89FD20650936F7E9442787, Portia47859CF5FF8AFC9721EEF5A600985A572F055CA4FA57B5D4FB578E7C08549EC9, Portia47FBF2CE655A0BFE041F48054259143EC9BF203612F24E82B9C8E3DC73E0056B, Portia4A57DD196CC4DFA74BA2A81FA6DD69B318AF958916620C3396D9D260326EE1B3, Portia4D86519FCC16F02548C3E29E580EC003867A07CF26D256F57E4B3D9A40816FFC, Portia4DD7D3410340405FBA257F32935EE3C1C844A4AB7348761CE6EC36FE0442CFEA, Portia4EFE49B1348AFC5BA81DF069FB9A0B89E1FBF2AD30994145DEF20BA105EBDEBE, Portia4F9472E8C89E75A49CAA5EB8FF7F639F8D013338A1A5DAFFE37FAD78E28C30DA, Portia4FFF06F1347F0DF9A617B1AA79C57C4BE787E4DE34BC7EED6A0E8052C8F5EF7C, Portia50C70F44CE8894CF7231272888D6062DE18FA19BF04DAC0834A26B9130C165A1, Portia51FE555DD79808725AEE8E82914094E523850FDA6EFBE755215A4071F1A05B52, Portia533F78EAEB8819DE62744BA4544C862D58319F328039FE16CF9904D0F81B6726, Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A, Portia5395E8EB93A7F927C06702E6F40680736BAFDF03A8CC1607A7345BA92295690E, Portia559C6B2CB453C53C337F5E8AA18127BB374E6308F310A0A099A30C3421B412A3, Portia5647C83C9923AF4B8A5A60D0E0D9CB27904F9C4571DEAE7F66B50E1FACD98453, Portia583B4F63A28210D35E65C0C2C3B23C1B843FF040116C8ED76F096FDA3FCE3137, Portia5AF07B3208EB9275DF00078FDEF65642C66304D924A78125731567AACD08E2A4, Portia5C08B534C036029E349B3D0CE114F3E24CAD136A7E31207CE6E7D5B26A094755, Portia5C785825C5C3F7E845CCBEE2767058870CB823F13882AECF91DBC1CBDA9F87FC, Portia5D4504657CF0F248BBB0D39DEB82554EBDBEFE27BFFEFBB869B53903B87F98DC, Portia5D9B3D88A76CFA247FEA271A78CAAC133EF00DBEBE6B5C8AC9431402F338F317, Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94, Portia613EC1E24171579C38DBADD1656E2EBACEFD2C2F65F7AE93AAB5427AD81797DE, Portia63D54EA89C5C07D310D2BCA13AD5F7B74227B5C1A92D8305F29836A899A420D7, Portia63F211009C81A9D69EBDC64C2FF858EA739198D57AE189F575F6DB29D8401827, Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA3161514, Portia67B9E95C80AD633362E30AED291963C74151F8CE6124A0F98697F8E3DCABB8D7, Portia67FF42154696CE62730A90222AB6C1618C3883A7B83A530679D7947EF82A7536, Portia687EE1C23460870912691E4FB708323EBC207241ED63637488F412D5496EE5EA, Portia68A3EA0CA5F80C9797BB122EE28E1022CC49939048D6F526CD8CA215FECE13B0, Portia68D897B5E51EC2FC3E79D0212B58DE2291C916C1893D2C59029BFF357274E581, Portia6AE7436CAF1D81D68D1CA26BCFADC44860AAA63B87A822CFB2212F1AE5375C3A, Portia6C1C6B7EB17FBC6FFD9D02B19FE9C2FA4FB3C6789E90B0635C3EE527EB8F1603, Portia6C9CF32EA0FA52D3E46677C05C4B09117A3F07A53F1D7BF0E3FD06FA6B3248BB, Portia6D456EA5A910F2BECBEC72F07A1EDDD7ACA1FD6D3C14C5F2B93DE61C863E48FD, Portia6EDEFDF3147D5333AAABFE7768FCC99D88F93C8F7C81708F0C551ABD6FD7A117, Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474, Portia717F77E4665D9616BE94EEEF72BC01EF932245408F2AEAE20394ADACE6A23687, Portia7289719EC72DE6F96317B0AF7168E28E848A0049DF1FA90D4E65F4328B1EC586, Portia73AADDF0B812FAE76C884FD50264381D369C3E9825947F115D6EE39E12C94DC6, Portia7447FB6BBB482B1C467364D016B654F11C2ED1628609E41058681ED0CB44705E, Portia74A5A31DE9F3A44C25116AA828DDAC667A065C414D865FF4FF6A5FC4C4C5A47A, Portia757E16DD9071C1EA6CD19C09CEF1B1E1BC16F3C168A35CA627AC8EFC9996C48D, Portia77BC6F20B8FD14DA873B0870E2E097EC40F2D759251FC89539FC397ABB0863C7, Portia78094BDF93BB27C5D0F9AA6EC6C971ACDDDD95176F34DADB21AF495C1971ED1D, Portia797165BE01568FE0B8DBC2E797503C47C5739072DA71A828AE545B546C5C11A2, Portia7A69D33280F826A4E8D05D28C507E4C68556897E2948CDFB8FF09D153ED9DF44, Portia7A8595D2FC8E586A820EE122D8C373E01E418CA296483244AB95A35A0943E149, Portia7B1AEE3985B856552CEAEAB86F86A8B046D256BB2EA8D4AB06B5F839FFA36B9F, Portia7B406DA21A6EBDB3B0EC40C4DEED4FE06FB99131586B6D1C6E24E83E42204F64, Portia7B41C47C738A8DA20460C8A8BE569630949FEA19EAB61C34DCBC49E83CDD123C, Portia7BA2123C497578ACF1569F2812DABDA302200FD1023402720CA66785EF762FED, Portia7C156CFCACA80768D1A08D2432719D7F1C4D591F3979C08C55586944821D80F5, Portia7F8B4C9203507BBF4A214CB378B625DDB003E856C25F722EAE639A78025702C1, Portia8165C4E0E3F218936EE79C52D55D60B2229FE25D669BF8BC3124D4D9E388B010, Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB, Portia84715FA1F92BA1E2F3F5CB5B59532DE980DF31373358E5412185F988E9D5FE48, Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C, Portia871F8CC05AD7D5F92C62179C2FAF4B0C9482F621AC1C5337DA051CA3449935F8, Portia88689C12C0FB6431DB31439CCE47EDDF15E892678804B4AAC1633CF025902FA4, Portia88B0F753F42B585D52BCAE1FC7E825AE67D21A190630E1D80A18A96727D1186F, Portia895E347267064464C7EEA3101D9897828DE9ACE40AE8D3EB6B31BD0E2E552E59, Portia8A10B3D9610CE4B221E33CC56E464200ACA1EE75D7D9944E4FE43F40B69577C1, Portia8D5633FE067F626EBE517251F6EFAB49E2AE1C79C435B49EA490311B53BCCF8A, Portia8EA37BD5888AD3C09F39A36A7FACB11427760BFA167EC4F8BAFBAEEF95F7ABE3, Portia8EC39D203E40F38A63A668A3300716B942ECA6885268F24789B7087CBB825751, Portia8F0CE391B931A838823BCA7B954533FA430653B8233464EE2D86EE0099BE798A, Portia9057974446C97EEBDB9DE36F30C6DB793B5538A21235EA3265B9649504F306BE, Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F7869, Portia92D234454C20DC8E7698D3F4608F463CC45108FD7C6FCB35EBDEFD7131274C0D, Portia96294E0FB33C6297223052ED7387F5E7FAA0C2A7CD4DC2CD6B9BFA4998A6D5C7, Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F, Portia96BF44EECD956DB8397701FC811AF70DE0D6E1ECDB8AB95318C6F10318CD1EDD, Portia97032B105C2A6D6A1B226A232D4A8D42B4A96F297375BA0A5D48F0938F15A14C, Portia9890D4629429B264BE737520EC815DFE7400C9A6ECC4B4451A28AF5E22133782, Portia98DBC88EDB4D5BDDE18F31C1436D60500C23C1F99805D1913C5915F5ADDC59DD, Portia999618C71B7B006783D3F23026F9C4D587F517B253F9F62802EFE693018FB2CA, Portia99A90254D5BFDBDC157B8B5C8D8360DE08AAA6B5160C5F1020B01CBABB085F66, Portia9B0607CA8CEF3829E2071107C5E2DC5C50730AFFBF2290344613FA16D3D34583, Portia9CA30FFC559321862CA47D0242119C614DCEC0E1E9443BE7873F142DB785415F, Portia9D06184903CEE42AA204A04AF68AF0059EF7DEDC923932BC599AB3BB26412113, Portia9EE7CFBC2DB1E57A7AB5450792B4056ACF5FDF2A4383256FD21D6EEEBF893879, Portia9FA5D5CF525C4AF42E939440C7466EDE599F60FC5B4D7162FD53DD072DBF2027, Portia9FCAF1CC602FB2404B5CF52AB3A7AF4FB9BF7AD634FD20E054362C2971D5C6E4, PortiaA25FBFD25C5DA718EF703EC9807E1F0349D507CDB2F63B9398B7F26E9EC369D9, PortiaA277510249DA33D1E77EA806415366BBFB478D775605D021833F8109CE070FCA, PortiaA4299F97F2F2793A334427A6DBE3AB10187D44660E16D667981207CF14AD4341, PortiaA575A714BD65E0A40C74D0ADDC7743F0FC29A5EC04226EA7371EBAD7B642251C, PortiaA6B51F7C7F08B507C488A94D38D27F9FD35725E3CB368F3DB31BA25BF1CBAE64, PortiaAB78B394BEAB9C3EAC62EC80E078A79A64B1AE85D3A626BAD90D554F0E0FF5F8, PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27, PortiaAC26148312D1D19C14B10EE61B64A0F265378CB38284D4E50CE0C3D7153A7508, PortiaACB074BCE785D34D94E14D208E190145EA345C44039DCEA40D390A0B5B303A9C, PortiaACD105735FFCA90DB185696163BF28386D9855A759F2E53F1B2B4A58AFE087CD, PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3, PortiaAD20397E4469C88F974BC9D5A386CD8E3C4A25CB034E5C0233899BA467B9C283, PortiaADD2B4C414E1944EE190450BDA58B841C185CE857DDB2F46E1785464B36D095B, PortiaAF9D90F9D29976C8CE67E2F682A350FB57A5B5B729FF40887ACC33E345E34BBC, PortiaAFAF766C0228CE70B8538430CAEDC481CB9225A47B3D7DB91672B60DE869A007, PortiaB2401AF589C5863F4C683B88251FE1B9E91B82E95AFAA57501CD8611540CBC1B, PortiaB40D0675C1B674F43BFBDCE25F20837F7CF28C29B2067DF1534D10FA9DE94F05, PortiaB47D44CD61F1BAE17F9B3642AC00CEF06F2C4D1DEB0364DFC7C6B68A7F1C6300, PortiaB4DD33A7F5FCF2E0B8851762FDE92625FD4111B116DCEEFF5492192AC3808FD1, PortiaB5702939DCF5C30F95C9A73E21F8F0F14F920AEA657BCC2634DED63C18886EEF, PortiaB5B4C6C1CCCDD997C7F390CEAD0680DD868403FA1B09E115245EBB04DFD8F886, PortiaB869DD0526AA33A1F47CE6CE38CC1B91E9DFADCED8528496D3AA2817193F8E4C, PortiaBE1636B313652DA76C47508EB54DDB2BC0EB3E2F94DD70683296AF4B4AC9B979, PortiaBE625002347E2BBF2D03CC7136B2D484C2DA3FD77DB41B321C10C3D110B761D3, PortiaBF61A0E2E929B0B0C2C8FD90D26A50B2912F90E0432F13057B32A47660B93B93, PortiaC166B2A6A50AB323EBE10D693B981A72966248C62D0DBCA17A9D1F8C2A0493E1, PortiaC39C98E2DBB3BF252193560530969F5DF01D0CB33395E2B40E2B8496665ED57A, PortiaC3C24AD50D924F9A1F4D0C270B605BEB0F1317AF1B9B674857E2967845D5EABE, PortiaC646B2FC53C967587683B193B991EC40E30A4137E8479560E880A6AAEA504244, PortiaC695C5A9B781750DCBCA504BA9AFE97AFE550E8F63FAF6ADE2DFE5B2E157C781, PortiaC6B58953EEC5AF0CF98AEC8E58EA963F8A1EF8B3D2BFA36F2F60E3073C7B1556, PortiaC95DF1FEF74AB78C247A61B1C7CA71C04A8A85C72948A02B997E3FFA2295B1A6, PortiaCAB9DEB40F4B7BA004A8D9BA3C94D8944AE60BFF46D6005CDD7D49FFE715CBA0, PortiaCADDBB36C737CF14F2057C0A4E15C9E63FDD811D019E6BEFA5647B9E2BA9E2C8, PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F, PortiaCCB8911E2FB70686013446C9D524FAAFA073B59022AB0B13FB7788B463ACDDE8, PortiaCDEB60612E2CCB73291DF472ACC26D72C213DF36B15FC7D666DCAD87F850A4F9, PortiaCE291342413ACA5E55E75E29F7C92CAA822AC0B201F64DA7A38365474F0782CD, PortiaCE317BDB9F287F13D0C5620C1C17559DBCA77AC9779C300BC881F44035E4A26D, PortiaD1911225671F0102A93760A010419412AB90210CE93B2AFC1084DF988DE0A198, PortiaD38366C0F7A014B7D38BCF2DDAEABAEB55C1BD6883615DFD34DDBA3D43322457, PortiaD493FB86F4D080CAC6A555893E11E63CE55AFFCBD6E771A8684B58B7C9536582, PortiaD56D35AC620E2A97A9B2A59A435078B6568941B2238932A20E4EABB153C1309C, PortiaD6BB547EF6FB03ADDD6888D292BD58FA5D2D19E4D01A028E872206CC71819530, PortiaD775A5D8BCBA1B953633C0582AECCE997439EE8D2C3F4CB8CF37FA9D5E33A873, PortiaD903C9F6E761AA93918B6873BE31012F4F4BD0B196CC911D5AE8BD35E6B778B0, PortiaDAC5690D56A8404A50FAA955F800B7A921C23C5147D404EFC243B5021316A557, PortiaDCF1EC4C8F6FE11762AB8AED23484BA812024D7817D6BDDEF7F58B3DFFA66AE3, PortiaDE57F78A9D37368116B1A7EC1CA79CF96CE81F200951380435CC4A669F8837DE, PortiaDF9CF8F410540161933C37D2952A4754D8E575F091E0F87B7AFD038C972A5738, PortiaE2F70B587A599F2A92FE1BC5C9941D3194A8ABA84334EB2D1BFDB23F21AF2C00, PortiaE34EE1C8D184A6D16AD1FE0849FC8B94B131A1A6FA453BB4C4FF29D2EC239B8C, PortiaE533A13177B62D1EFE8E47B2C8DBE1B703CC5A9D68DD56C8A5015618FD353625, PortiaE696B3EC1B513A6E3725F1903D7BE6C03EAA06E8EC473F8555A1F39F198BB262, PortiaE743D385FF26935D87C27AF6254C90A88FE79C15319CDD62451780019AF8EDBD, PortiaE7F9DFDF55BC2B0ECD9A9FF37523ADE10CA787A7D4B97D66CCE5EAF13D29AFA4, PortiaE86118FEDCAAD9C8EECB6F0820286060A99630C598D8AE761402890F6BA8A2E2, PortiaE9B2575F51D37D152BB376D660B9FD59648CDADCCFB631542854BC97BC5CBA7D, PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54, PortiaEBEBAE40CDBFE8B4726C2E5FDB6EE27438942900064BA238E5F7452B92135107, PortiaEC8A68FE610BE733B5FB5E10D0C0A79743573CEEBB9114A5BBD9B4542987EC3B, PortiaED1DF65949C6DF6D7D9CCF63B9EE97A054381BD875B459C20A7BD5339B9C7626, PortiaED3F7B7FEBE1F20B8D98DAE3ABE7516820F289A5DFA781A7229620B1EC2C9BF7, PortiaEED978F2141A5821BB72161402A02FDBDB787544E177B8457CBD97CE06CEB5F3, PortiaEFE869FF2C47D1239F76963FA0AB021FCF0B3A5037DC8506F1AE7FF602338979, PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54, PortiaF38F4E93C39265245C5ADAB3FA4776FF096BEF74C5FFABFD2BC49604E5DD0416, PortiaF3BACE104C01FC9709E1FE05FA74F9ED4D50EA2EACF73C77510A4B743F9B8CC6, PortiaF5D86DD63563039EDAF243CA29FEAE85382ED90210D8D752F40BC3E24D4ED319, PortiaF7F18D546C1EB99D8AF0A43E4B93EF618B497FF7B4FC9EE8E09418D29FD14BD0, PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA1289, PortiaF91217E35959EE564AF2004EA1727032FC5084CF5CF0155ED5D422DB017C5B3F, PortiaF9B77C7ADBEF0EB8FE1C2659269AA790D9F0D90014FCAFB7B2571FB108D35FD8, PortiaFA8797A2099AD3C0E34408B3661A645092B14759512BA3656FC05B7770BF1E26, PortiaFB363E63265F366744A1615DFD1F101B4EADC47287216F1A34FA3DACB525E2AF, PortiaFCF00D94A0472AF13CDF0CB77F1A463EF3413B0EDADF00C16BFD3C56766F97C2, PortiaFDFB5EB07BD18FCE44E06DBD37F46C64A2C4C2935EAE7D0785664C275FB1204A, PortiaFF23C6AFCFE989210F90258F60DB04711DE3E6CCC9A94A0EF8BEC518127F86BE, PortiaFF553F232A8414665BAE406E202CCC0CC27AC9885754BE84D83F540E440E0733 } from "./schemas";
import type { AcceptAccessPopulationPath, AcceptRiskPath, AcceptTenantInvitationPath, AcknowledgePolicyPath, AddCorrectiveActionPath, AmendProgramScopeSnapshotPath, AmendWorkforceRosterSnapshotPath, AnnotateReadinessGapPath, ApproveAccessExpectationPath, ApproveBoundaryPath, ApproveCampaignWaiverPath, ApproveCommitmentDraftPath, ApproveControlOperatingPlanPath, ApproveControlPath, ApprovePolicyPath, ApprovePolicyRetirementPath, ApproveSeparationOfDutiesWaiverPath, AssignResponsibilityPath, AssignRiskOwnerPath, AssignRolePermissionPath, AssignTeamMemberPath, AssignTeamRolePath, AssignWorkItemPath, AttestControlOccurrencePath, CancelApplicationImportPath, ChangeTenantSlugPath, ChooseRiskTreatmentPath, ClaimWorkItemPath, ClassifyAccessPrincipalPath, CloseCampaignPath, CloseFindingPath, CompleteAccessReviewCampaignPath, CompleteCorrectiveActionPath, CompleteEmailChallengePath, ConfirmPolicyReviewPath, CorrectControlAttestationPath, CorrelatePersonMembershipPath, CreateBoundaryPath, CreateClientServicePath, CreateCommitmentDraftPath, CreateControlDraftPath, CreatePolicyDraftPath, CreateProgramPath, CreateRiskDraftPath, DecideAccessReviewScopePath, DecideReadinessPath, DecideTypeIEntryPath, DeclareApplicationPath, DeclareSystemInstancePath, DefineRolePath, DefineTeamPath, DefineTrainingRequirementPath, DelegateWorkItemPath, DeleteRolePath, DeleteTeamPath, DesignateControlOwnerPersonPath, DiscardBoundaryDraftPath, DiscardControlDraftPath, DiscardPolicyDraftPath, DisposeControlEvaluationDeviationPath, EscalateWorkItemPath, ExemptAccessExpectationPath, ExemptAccessRemediationPath, ExemptMissingAccessPopulationPath, FreezeProgramScopeSnapshotPath, FreezeWorkforceRosterSnapshotPath, GetAccessPopulationPath, GetAccessReviewCampaignPath, GetAccessReviewCoveragePath, GetAccessReviewCoverageQuery, GetAccessReviewScopePath, GetAccessReviewScopeQuery, GetAccessVariancePath, GetApplicationImportPath, GetApplicationImportQuery, GetApplicationPath, GetApplicationQuery, GetApplicationRevisionPath, GetBoundaryDecisionPath, GetBoundaryPath, GetBoundaryQuery, GetBoundaryVersionPath, GetBoundaryVersionQuery, GetCampaignPath, GetCampaignQuery, GetClientServicePath, GetClientServiceQuery, GetClientServiceRevisionPath, GetCommitmentDraftPath, GetCommitmentDraftQuery, GetCommitmentDraftRevisionPath, GetCommitmentVersionPath, GetControlCriterionMappingPath, GetControlDecisionPath, GetControlDraftPath, GetControlDraftQuery, GetControlDraftRevisionPath, GetControlEvaluationPath, GetControlOccurrencePath, GetControlOperatingPlanPath, GetControlVersionPath, GetCriteriaCatalogEditionPath, GetCriteriaCatalogEntryPath, GetCriterionApplicabilityPath, GetCurrentControlVersionPath, GetDataFlowPath, GetDataFlowQuery, GetEffectiveBoundaryVersionPath, GetEffectiveBoundaryVersionQuery, GetEffectiveCommitmentVersionPath, GetEffectiveCommitmentVersionQuery, GetEffectiveControlVersionPath, GetEffectiveControlVersionQuery, GetEffectivePolicyVersionPath, GetEffectivePolicyVersionQuery, GetEmailAddressPath, GetEmailChallengeStatusPath, GetFindingPath, GetInformationAssetPath, GetInformationAssetQuery, GetMemberAccessPath, GetMemberAccessQuery, GetPersonPath, GetPersonQuery, GetPolicyPath, GetPolicyVersionPath, GetProgramPath, GetProgramQuery, GetProgramRevisionPath, GetProgramSetupWorkPath, GetProgramSetupWorkQuery, GetReadinessAssessmentPath, GetRiskDraftPath, GetRiskDraftQuery, GetRiskDraftRevisionPath, GetRiskEvaluationPath, GetRiskEvaluationQuery, GetRiskGovernancePath, GetRiskMethodPath, GetRiskMethodVersionPath, GetRolePath, GetSeparationOfDutiesWaiverPath, GetServiceIdentityPath, GetServiceIdentityQuery, GetSnapshotPath, GetSnapshotQuery, GetSystemInstancePath, GetSystemInstanceQuery, GetTeamPath, GetTechnologyComponentPath, GetTechnologyComponentQuery, GetTenantMemberPath, GetTenantPath, GetTrainingRequirementPath, GetTrainingRequirementQuery, GetWorkDigestPath, GetWorkDigestPreferencePath, GetWorkItemPath, GetWorkRelationshipPath, GetWorkRelationshipQuery, GetWorkforceRosterSnapshotAsOfPath, GetWorkforceRosterSnapshotAsOfQuery, GetWorkforceRosterSnapshotPath, GrantAccessPath, InviteOrganizationMemberPath, InviteTenantMemberPath, IssueEmailChallengePath, LaunchAccessReviewCampaignPath, LaunchPolicyCampaignPath, LaunchTrainingCampaignPath, LinkFindingAcceptancePath, ListAccessExpectationsPath, ListAccessGrantsPath, ListAccessPopulationsPath, ListAccessPopulationsQuery, ListAccessPrincipalsPath, ListAccessPrincipalsQuery, ListAccessReviewCampaignsPath, ListAccessReviewCampaignsQuery, ListAccessReviewScopesPath, ListAccessReviewScopesQuery, ListApplicationBoundaryReferencesPath, ListApplicationBoundaryReferencesQuery, ListApplicationImportRowsPath, ListApplicationImportRowsQuery, ListApplicationRevisionsPath, ListApplicationRevisionsQuery, ListApplicationsPath, ListApplicationsQuery, ListBoundaryDecisionsPath, ListBoundaryDecisionsQuery, ListBoundaryVersionsPath, ListBoundaryVersionsQuery, ListCampaignAmendmentsPath, ListCampaignAmendmentsQuery, ListCampaignParticipantsPath, ListCampaignParticipantsQuery, ListCampaignsPath, ListCampaignsQuery, ListClientServiceRevisionsPath, ListClientServiceRevisionsQuery, ListClientServicesPath, ListClientServicesQuery, ListCommitmentDecisionsPath, ListCommitmentDecisionsQuery, ListCommitmentDraftRevisionsPath, ListCommitmentDraftRevisionsQuery, ListCommitmentDraftsPath, ListCommitmentDraftsQuery, ListCommitmentVersionsPath, ListCommitmentVersionsQuery, ListControlCriterionMappingsPath, ListControlCriterionMappingsQuery, ListControlDecisionsPath, ListControlDecisionsQuery, ListControlDraftRevisionsPath, ListControlDraftRevisionsQuery, ListControlDraftsPath, ListControlDraftsQuery, ListControlEvaluationsPath, ListControlEvaluationsQuery, ListControlOccurrencesPath, ListControlOccurrencesQuery, ListControlOperatingBlockersPath, ListControlOperatingBlockersQuery, ListControlVersionsPath, ListControlVersionsQuery, ListCriteriaCatalogEditionsPath, ListCriteriaCatalogEntriesPath, ListCriteriaCatalogEntriesQuery, ListCriteriaCoveragePath, ListCriteriaCoverageQuery, ListCriterionApplicabilityPath, ListCriterionApplicabilityQuery, ListDataFlowRevisionsPath, ListDataFlowRevisionsQuery, ListDataFlowsPath, ListDataFlowsQuery, ListEmailAddressesPath, ListEmailAddressesQuery, ListFindingsPath, ListFindingsQuery, ListInformationAssetRevisionsPath, ListInformationAssetRevisionsQuery, ListInformationAssetsPath, ListInformationAssetsQuery, ListMemberResponsibilitiesPath, ListMyControlWorkPath, ListMyControlWorkQuery, ListMyTenantsQuery, ListPeoplePath, ListPeopleQuery, ListPoliciesPath, ListPoliciesQuery, ListPolicyDecisionsPath, ListPolicyDecisionsQuery, ListPolicyVersionsPath, ListPolicyVersionsQuery, ListProgramBoundariesPath, ListProgramBoundariesQuery, ListProgramClientServicesPath, ListProgramClientServicesQuery, ListProgramRevisionsPath, ListProgramRevisionsQuery, ListProgramSnapshotsPath, ListProgramSnapshotsQuery, ListProgramsPath, ListProgramsQuery, ListReadinessAnnotationsPath, ListReadinessAnnotationsQuery, ListReadinessAssessmentsPath, ListReadinessAssessmentsQuery, ListReadinessGapsPath, ListReadinessGapsQuery, ListResponsibilitiesPath, ListResponsibilitiesQuery, ListRiskDraftRevisionsPath, ListRiskDraftRevisionsQuery, ListRiskDraftsPath, ListRiskDraftsQuery, ListRiskEvaluationHistoryPath, ListRiskEvaluationHistoryQuery, ListRolePermissionsPath, ListRolePermissionsQuery, ListRoleTeamsPath, ListRoleTeamsQuery, ListRolesPath, ListRolesQuery, ListServiceIdentitiesPath, ListServiceIdentitiesQuery, ListSystemInstanceBoundaryReferencesPath, ListSystemInstanceBoundaryReferencesQuery, ListSystemInstancesPath, ListSystemInstancesQuery, ListTeamMembersPath, ListTeamMembersQuery, ListTeamRolesPath, ListTeamRolesQuery, ListTeamsPath, ListTeamsQuery, ListTechnologyComponentRevisionsPath, ListTechnologyComponentRevisionsQuery, ListTechnologyComponentsPath, ListTechnologyComponentsQuery, ListTenantInvitationsPath, ListTenantInvitationsQuery, ListTenantMembersPath, ListTenantMembersQuery, ListTenantsQuery, ListTrainingRequirementsPath, ListTrainingRequirementsQuery, ListTypeIEntryDecisionsPath, ListTypeIEntryDecisionsQuery, ListWorkPath, ListWorkQuery, ListWorkRelationshipsPath, ListWorkRelationshipsQuery, ListWorkRemindersPath, ListWorkforceObservationsPath, ListWorkforceObservationsQuery, ListWorkforceReconciliationObservationsPath, ListWorkforceReconciliationObservationsQuery, ListWorkforceRosterSnapshotsPath, ListWorkforceRosterSnapshotsQuery, OpenAccessPopulationPath, OpenControlOccurrencePath, PlanReadinessGapPath, PreviewAccessPopulationPath, PreviewApplicationChangePath, PreviewApplicationImportPath, PreviewApplicationImportQuery, PreviewBoundaryImpactPath, PreviewBoundaryImpactQuery, PreviewBulkAccessDecisionPath, PreviewCommitmentImpactPath, PreviewCommitmentImpactQuery, PreviewControlImpactPath, PreviewControlImpactQuery, PreviewControlOperatingPlanPath, PreviewInformationAssetChangePath, PreviewPolicyImpactPath, PreviewPolicyImpactQuery, PreviewResponsibilityConflictsPath, PreviewResponsibilityConflictsQuery, ProposeAccessExpectationPath, ProposeBoundarySuccessorPath, ProposeControlCriterionMappingPath, ProposeControlOperatingPlanPath, ProposeControlRetirementPath, ProposeControlSuccessorPath, ProposeCriterionNotApplicablePath, ProposePolicyRetirementPath, ProposePolicySuccessorPath, ProposeRiskControlTreatmentPath, PublishRiskMethodVersionPath, RaiseFindingPath, ReactivateTenantPath, ReconcileCampaignAudiencePath, RecordAccessDecisionPath, RecordAccessPopulationFactsPath, RecordAccessRemediationChangePath, RecordBulkAccessDecisionPath, RecordControlEvaluationStepPath, RecordDataFlowPath, RecordInformationAssetPath, RecordPersonPath, RecordRiskAssessmentPath, RecordSeparationOfDutiesWaiverPath, RecordServiceIdentityPath, RecordTechnologyComponentPath, RecordTrainingCompletionPath, RecordWorkRelationshipPath, RegenerateProgramScopeSnapshotManifestPath, RegenerateWorkforceRosterSnapshotManifestPath, ReinstateMemberPath, RemoveRolePermissionPath, RemoveTeamMemberPath, RemoveTeamRolePath, ReopenFindingPath, ReserveEmailPath, ResolveMyTenantSlugPath, ResolveWorkforceObservationPath, RetireApplicationPath, RetireClientServicePath, RetireControlCriterionMappingPath, RetireControlPath, RetireRiskControlTreatmentPath, RetireSystemInstancePath, ReviewBoundaryPath, ReviewCommitmentDraftPath, ReviewControlCriterionMappingPath, ReviewControlEvaluationPath, ReviewControlOccurrencePath, ReviewControlPath, ReviewCriterionApplicabilityPath, ReviewPolicyDraftPath, ReviewRiskControlTreatmentPath, ReviseApplicationPath, ReviseBoundaryDraftPath, ReviseClientServicePath, ReviseCommitmentDraftPath, ReviseControlDraftPath, ReviseDataFlowPath, ReviseFindingPath, ReviseInformationAssetPath, RevisePersonPath, RevisePolicyDraftPath, ReviseProgramPath, ReviseRiskDraftPath, ReviseServiceIdentityPath, ReviseTechnologyComponentPath, ReviseTrainingRequirementPath, ReviseWorkRelationshipPath, RevokeAccessGrantPath, RevokeResponsibilityPath, RunReadinessAssessmentPath, SelectProgramCriteriaEditionPath, SetWorkDigestPreferencePath, StageApplicationImportPath, StartControlEvaluationPath, SubmitControlEvaluationPath, SuspendMemberPath, SuspendTenantPath, VerifyAccessRemediationPath, VerifyProgramScopeSnapshotPath, WithdrawControlProposalPath, WithdrawCriterionNotApplicablePath } from "./operations";

export const api = defineApi({
  continueWithDeveloperIdentity: post("/api/v1/developer-user-sessions")
    .body(json<{
  "email_address": string | null;
}>())
    .returns(json<Portia70B24E90A73EA0794A4DD21F876262A340BEE644ACBC9B8FF2F0890A79B9D474>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  grantPlatformOperator: post("/api/v1/platform/operator-grants")
    .body(json<{
  "user_id": string;
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  revokePlatformOperator: post("/api/v1/platform/operator-revocations")
    .body(json<{
  "user_id": string;
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPlatformOperators: get("/api/v1/platform/operators")
    .returns(json<Portia0BBEEB50D9C1BAC9B12E86679F1B76D5365AB62E7358C3F3CBF8735D3BE93FDA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTenants: get("/api/v1/platform/tenants")
    .query<ListTenantsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia166CE150C5FA11C2FF07F303878BE1D989853E482321CEFA07EC5F3D792CC0DF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  resolveMyTenantSlug: get("/api/v1/tenant-slugs/{slug}/mine")
    .params<ResolveMyTenantSlugPath>({ "slug": { style: "simple", explode: false } })
    .returns(json<Portia6AE7436CAF1D81D68D1CA26BCFADC44860AAA63B87A822CFB2212F1AE5375C3A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  registerTenant: post("/api/v1/tenants")
    .body(json<{
  "name": string | null;
  "slug": string | null;
  "legal_name"?: string | null;
  "first_administrator_email"?: string | null;
}>())
    .returns(json<Portia5DC40A4F1CB15D96F9110C5B5E53AA99EDE859D42D78207E7C39CA076C05FE94>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listMyTenants: get("/api/v1/tenants/mine")
    .query<ListMyTenantsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia583B4F63A28210D35E65C0C2C3B23C1B843FF040116C8ED76F096FDA3FCE3137>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTenant: get("/api/v1/tenants/{tenant_id}")
    .params<GetTenantPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia533F78EAEB8819DE62744BA4544C862D58319F328039FE16CF9904D0F81B6726>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessGrants: get("/api/v1/tenants/{tenant_id}/access-grants")
    .params<ListAccessGrantsPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia84715FA1F92BA1E2F3F5CB5B59532DE980DF31373358E5412185F988E9D5FE48>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  grantAccess: post("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}")
    .params<GrantAccessPath>({ "grant_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "proposal": PortiaD493FB86F4D080CAC6A555893E11E63CE55AFFCBD6E771A8684B58B7C9536582;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  revokeAccessGrant: del("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}")
    .params<RevokeAccessGrantPath>({ "grant_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  openAccessPopulation: post("/api/v1/tenants/{tenant_id}/access-populations")
    .params<OpenAccessPopulationPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "application_id": string;
  "system_instance_id": string;
  "expected_system_instance_revision": number | string;
  "observed_at": string;
  "source": string | null;
}>())
    .returns(json<Portia78094BDF93BB27C5D0F9AA6EC6C971ACDDDD95176F34DADB21AF495C1971ED1D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getAccessPopulation: get("/api/v1/tenants/{tenant_id}/access-populations/{population_id}")
    .params<GetAccessPopulationPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia999618C71B7B006783D3F23026F9C4D587F517B253F9F62802EFE693018FB2CA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  acceptAccessPopulation: post("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance")
    .params<AcceptAccessPopulationPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "attestation": string | null;
}>())
    .returns(json<Portia8D5633FE067F626EBE517251F6EFAB49E2AE1C79C435B49EA490311B53BCCF8A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  classifyAccessPrincipal: post("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications")
    .params<ClassifyAccessPrincipalPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "provider_subject_id": string | null;
  "expected_classification_count": number | string;
  "classification": string | null;
  "rationale": string | null;
  "person_id"?: string;
  "service_identity_id"?: string;
  "accountable_owner_person_id"?: string;
  "shared_justification"?: string | null;
}>())
    .returns(json<Portia5395E8EB93A7F927C06702E6F40680736BAFDF03A8CC1607A7345BA92295690E>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordAccessPopulationFacts: put("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/facts")
    .params<RecordAccessPopulationFactsPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "principals": PortiaFF23C6AFCFE989210F90258F60DB04711DE3E6CCC9A94A0EF8BEC518127F86BE;
  "entitlements": PortiaAFAF766C0228CE70B8538430CAEDC481CB9225A47B3D7DB91672B60DE869A007;
  "group_members": PortiaE34EE1C8D184A6D16AD1FE0849FC8B94B131A1A6FA453BB4C4FF29D2EC239B8C;
  "assignments": Portia63F211009C81A9D69EBDC64C2FF858EA739198D57AE189F575F6DB29D8401827;
}>())
    .returns(json<Portia78094BDF93BB27C5D0F9AA6EC6C971ACDDDD95176F34DADB21AF495C1971ED1D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewAccessPopulation: get("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/preview")
    .params<PreviewAccessPopulationPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaB4DD33A7F5FCF2E0B8851762FDE92625FD4111B116DCEEFF5492192AC3808FD1>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessPrincipals: get("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/principals")
    .params<ListAccessPrincipalsPath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListAccessPrincipalsQuery>({ "cursor": { style: "form", explode: true }, "gap": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia8EA37BD5888AD3C09F39A36A7FACB11427760BFA167EC4F8BAFBAEEF95F7ABE3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getAccessVariance: get("/api/v1/tenants/{tenant_id}/access-populations/{population_id}/variance")
    .params<GetAccessVariancePath>({ "population_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaD38366C0F7A014B7D38BCF2DDAEABAEB55C1BD6883615DFD34DDBA3D43322457>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessReviewCampaigns: get("/api/v1/tenants/{tenant_id}/access-review-campaigns")
    .params<ListAccessReviewCampaignsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListAccessReviewCampaignsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaBF61A0E2E929B0B0C2C8FD90D26A50B2912F90E0432F13057B32A47660B93B93>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  launchAccessReviewCampaign: post("/api/v1/tenants/{tenant_id}/access-review-campaigns")
    .params<LaunchAccessReviewCampaignPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
  "instructions": string | null;
  "deadline": string;
  "assignments": Portia7BA2123C497578ACF1569F2812DABDA302200FD1023402720CA66785EF762FED;
}>())
    .returns(json<PortiaB869DD0526AA33A1F47CE6CE38CC1B91E9DFADCED8528496D3AA2817193F8E4C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getAccessReviewCampaign: get("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}")
    .params<GetAccessReviewCampaignPath>({ "campaign_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia67B9E95C80AD633362E30AED291963C74151F8CE6124A0F98697F8E3DCABB8D7>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewBulkAccessDecision: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decision-previews")
    .params<PreviewBulkAccessDecisionPath>({ "campaign_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "item_ids": Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
  "decision": string | null;
}>())
    .returns(json<Portia77BC6F20B8FD14DA873B0870E2E097EC40F2D759251FC89539FC397ABB0863C7>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordBulkAccessDecision: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions")
    .params<RecordBulkAccessDecisionPath>({ "campaign_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "item_ids": Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
  "decision": string | null;
  "rationale": string | null;
  "preview_token": string | null;
}>())
    .returns(json<Portia3A00A9545DBC606A9D844545423CA77D102CDF0448DC82A4001C72CD45E3849A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  completeAccessReviewCampaign: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion")
    .params<CompleteAccessReviewCampaignPath>({ "campaign_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "attestation": string | null;
}>())
    .returns(json<Portia4745E5442B58B410B21973D635D16710CD3A17B4B309CF195296D53640E3785B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordAccessDecision: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions")
    .params<RecordAccessDecisionPath>({ "campaign_id": { style: "simple", explode: false }, "item_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "decision": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia8F0CE391B931A838823BCA7B954533FA430653B8233464EE2D86EE0099BE798A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordAccessRemediationChange: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes")
    .params<RecordAccessRemediationChangePath>({ "campaign_id": { style: "simple", explode: false }, "item_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "reference": string | null;
  "description": string | null;
  "changed_at": string;
}>())
    .returns(json<Portia797165BE01568FE0B8DBC2E797503C47C5739072DA71A828AE545B546C5C11A2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  exemptAccessRemediation: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions")
    .params<ExemptAccessRemediationPath>({ "campaign_id": { style: "simple", explode: false }, "item_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
  "expires_at"?: string;
}>())
    .returns(json<PortiaDCF1EC4C8F6FE11762AB8AED23484BA812024D7817D6BDDEF7F58B3DFFA66AE3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  verifyAccessRemediation: post("/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications")
    .params<VerifyAccessRemediationPath>({ "campaign_id": { style: "simple", explode: false }, "item_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "population_id": string;
}>())
    .returns(json<Portia68A3EA0CA5F80C9797BB122EE28E1022CC49939048D6F526CD8CA215FECE13B0>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  stageApplicationImport: post("/api/v1/tenants/{tenant_id}/application-imports")
    .params<StageApplicationImportPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "submission_id": string;
  "source_key": string | null;
  "source_namespace": string | null;
  "coverage": string | null;
  "rows": PortiaC695C5A9B781750DCBCA504BA9AFE97AFE550E8F63FAF6ADE2DFE5B2E157C781;
}>())
    .returns(json<PortiaA6B51F7C7F08B507C488A94D38D27F9FD35725E3CB368F3DB31BA25BF1CBAE64>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getApplicationImport: get("/api/v1/tenants/{tenant_id}/application-imports/{batch_id}")
    .params<GetApplicationImportPath>({ "batch_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetApplicationImportQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaEED978F2141A5821BB72161402A02FDBDB787544E177B8457CBD97CE06CEB5F3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  cancelApplicationImport: post("/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations")
    .params<CancelApplicationImportPath>({ "batch_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_batch_revision": number | string;
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewApplicationImport: get("/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview")
    .params<PreviewApplicationImportPath>({ "batch_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<PreviewApplicationImportQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia42415CD397320D0C15EC3283D7AB52C15BCC5C24CB26307A7BCA7CB15F1D7ABF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listApplicationImportRows: get("/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows")
    .params<ListApplicationImportRowsPath>({ "batch_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListApplicationImportRowsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaD56D35AC620E2A97A9B2A59A435078B6568941B2238932A20E4EABB153C1309C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listApplications: get("/api/v1/tenants/{tenant_id}/applications")
    .params<ListApplicationsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListApplicationsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia3A795A94620F3B58B5F1452221FC679AB9B2B1CA5FD22BC2E89D277F4D764F4B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  declareApplication: post("/api/v1/tenants/{tenant_id}/applications")
    .params<DeclareApplicationPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
  "purpose": string | null;
  "owner_reference"?: string | null;
  "classification"?: string | null;
  "system_owner_person_id"?: string;
  "access_owner_person_id"?: string;
}>())
    .returns(json<Portia1D599653835771B5B38C77F6C963CA25D736248D35B45B1CD6CED58C58E70B11>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getApplication: get("/api/v1/tenants/{tenant_id}/applications/{application_id}")
    .params<GetApplicationPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetApplicationQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaCE317BDB9F287F13D0C5620C1C17559DBCA77AC9779C300BC881F44035E4A26D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseApplication: put("/api/v1/tenants/{tenant_id}/applications/{application_id}")
    .params<ReviseApplicationPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "name": string | null;
  "purpose": string | null;
  "owner_reference"?: string | null;
  "classification"?: string | null;
  "system_owner_person_id"?: string;
  "access_owner_person_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getAccessReviewCoverage: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-coverage")
    .params<GetAccessReviewCoveragePath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetAccessReviewCoverageQuery>({ "as_of": { style: "form", explode: true } })
    .returns(json<PortiaD1911225671F0102A93760A010419412AB90210CE93B2AFC1084DF988DE0A198>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessReviewScopes: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-scopes")
    .params<ListAccessReviewScopesPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListAccessReviewScopesQuery>({ "as_of": { style: "form", explode: true }, "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaE696B3EC1B513A6E3725F1903D7BE6C03EAA06E8EC473F8555A1F39F198BB262>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listApplicationBoundaryReferences: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary-references")
    .params<ListApplicationBoundaryReferencesPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListApplicationBoundaryReferencesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaED1DF65949C6DF6D7D9CCF63B9EE97A054381BD875B459C20A7BD5339B9C7626>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewApplicationChange: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews")
    .params<PreviewApplicationChangePath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_application_revision": number | string;
  "change_kind": string | null;
  "name"?: string | null;
  "purpose"?: string | null;
  "owner_reference"?: string | null;
  "classification"?: string | null;
}>())
    .returns(json<PortiaC166B2A6A50AB323EBE10D693B981A72966248C62D0DBCA17A9D1F8C2A0493E1>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireApplication: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/retirements")
    .params<RetireApplicationPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "effective_at": string;
  "reason": string | null;
  "merged_into_application_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listApplicationRevisions: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions")
    .params<ListApplicationRevisionsPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListApplicationRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_application_revision": { style: "form", explode: true } })
    .returns(json<Portia73AADDF0B812FAE76C884FD50264381D369C3E9825947F115D6EE39E12C94DC6>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getApplicationRevision: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}")
    .params<GetApplicationRevisionPath>({ "application_id": { style: "simple", explode: false }, "revision": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia6D456EA5A910F2BECBEC72F07A1EDDD7ACA1FD6D3C14C5F2B93DE61C863E48FD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listSystemInstances: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances")
    .params<ListSystemInstancesPath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListSystemInstancesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_application_revision": { style: "form", explode: true } })
    .returns(json<Portia33A914476F5916BA05D9CCA32D0BA6790B75A8EBFC139D7F65E58BAF1716FF4D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  declareSystemInstance: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances")
    .params<DeclareSystemInstancePath>({ "application_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_application_revision": number | string;
  "name": string | null;
  "kind": string | null;
  "access_boundary_reference"?: string | null;
  "source_identifier"?: string | null;
}>())
    .returns(json<PortiaBE625002347E2BBF2D03CC7136B2D484C2DA3FD77DB41B321C10C3D110B761D3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getSystemInstance: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}")
    .params<GetSystemInstancePath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetSystemInstanceQuery>({ "minimum_application_revision": { style: "form", explode: true }, "minimum_instance_revision": { style: "form", explode: true } })
    .returns(json<PortiaCDEB60612E2CCB73291DF472ACC26D72C213DF36B15FC7D666DCAD87F850A4F9>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeAccessExpectation: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations")
    .params<ProposeAccessExpectationPath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_ledger_revision": number | string;
  "rule_kind": string | null;
  "parameters": Portia1C25C4B0018EA4C94541F8463FC978A3F4C63744AC2F9FC2D98FEE5B6F55AA8E;
  "rationale": string | null;
  "effective_from": string;
  "effective_until"?: string;
  "supersedes_expectation_id"?: string;
}>())
    .returns(json<Portia1FF79BD41EDC10E67AF82C5AC8AA375E30EB20870D88CD7322218F9CD2983831>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  exemptMissingAccessPopulation: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions")
    .params<ExemptMissingAccessPopulationPath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_ledger_revision": number | string;
  "reason": string | null;
  "expires_at": string;
}>())
    .returns(json<PortiaF7F18D546C1EB99D8AF0A43E4B93EF618B497FF7B4FC9EE8E09418D29FD14BD0>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getAccessReviewScope: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope")
    .params<GetAccessReviewScopePath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetAccessReviewScopeQuery>({ "as_of": { style: "form", explode: true } })
    .returns(json<Portia7A8595D2FC8E586A820EE122D8C373E01E418CA296483244AB95A35A0943E149>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  decideAccessReviewScope: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions")
    .params<DecideAccessReviewScopePath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_system_instance_revision": number | string;
  "expected_decision_count": number | string;
  "decision": string | null;
  "reason": string | null;
  "effective_from": string;
  "review_by"?: string;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia1A72141BBDF886D72A311BA94564A77E3CFA5B0DBA1A3FD78CF78C689F1F779D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listSystemInstanceBoundaryReferences: get("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/boundary-references")
    .params<ListSystemInstanceBoundaryReferencesPath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListSystemInstanceBoundaryReferencesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaED1DF65949C6DF6D7D9CCF63B9EE97A054381BD875B459C20A7BD5339B9C7626>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireSystemInstance: post("/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements")
    .params<RetireSystemInstancePath>({ "application_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "effective_at": string;
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getBoundary: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}")
    .params<GetBoundaryPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetBoundaryQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia7B406DA21A6EBDB3B0EC40C4DEED4FE06FB99131586B6D1C6E24E83E42204F64>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listBoundaryDecisions: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions")
    .params<ListBoundaryDecisionsPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListBoundaryDecisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia17488FFB4D3C927EDA1FEEAABE0836C927B726E447755DAE04EE2C5CFE567A82>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getBoundaryDecision: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions/{decision_id}")
    .params<GetBoundaryDecisionPath>({ "boundary_id": { style: "simple", explode: false }, "decision_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia2D5CCE34626A84B45EE900195D49320E848D2908BBBBF8C24AF6A2EAF51BBCBA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseBoundaryDraft: put("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}")
    .params<ReviseBoundaryDraftPath>({ "boundary_id": { style: "simple", explode: false }, "draft_version_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "content": Portia1E60B56A95A64AF1BB8BE85BC8B2174D1CD473A879DA9B1370C87F09E86C448F;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveBoundary: post("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals")
    .params<ApproveBoundaryPath>({ "boundary_id": { style: "simple", explode: false }, "draft_version_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "effective_from": string;
  "rationale": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  discardBoundaryDraft: post("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards")
    .params<DiscardBoundaryDraftPath>({ "boundary_id": { style: "simple", explode: false }, "draft_version_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewBoundaryImpact: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview")
    .params<PreviewBoundaryImpactPath>({ "boundary_id": { style: "simple", explode: false }, "draft_version_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<PreviewBoundaryImpactQuery>({ "expected_revision": { style: "form", explode: true } })
    .returns(json<Portia3C2330B1E721B3907D876EAF59B8BADC89F6106BC2A83EBC53F96A325C8F9D64>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewBoundary: post("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews")
    .params<ReviewBoundaryPath>({ "boundary_id": { style: "simple", explode: false }, "draft_version_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEffectiveBoundaryVersion: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version")
    .params<GetEffectiveBoundaryVersionPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetEffectiveBoundaryVersionQuery>({ "effective_on": { style: "form", explode: true }, "minimum_boundary_revision": { style: "form", explode: true } })
    .returns(json<Portia4EFE49B1348AFC5BA81DF069FB9A0B89E1FBF2AD30994145DEF20BA105EBDEBE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeBoundarySuccessor: post("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors")
    .params<ProposeBoundarySuccessorPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_approved_version_id": string;
  "content": Portia1E60B56A95A64AF1BB8BE85BC8B2174D1CD473A879DA9B1370C87F09E86C448F;
}>())
    .returns(json<Portia0FBE6D4CEB2CC84764DF0FFF82C76BB333FB69E323061CA548534BC3DFB78B51>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listBoundaryVersions: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions")
    .params<ListBoundaryVersionsPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListBoundaryVersionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_boundary_revision": { style: "form", explode: true } })
    .returns(json<Portia99A90254D5BFDBDC157B8B5C8D8360DE08AAA6B5160C5F1020B01CBABB085F66>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getBoundaryVersion: get("/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}")
    .params<GetBoundaryVersionPath>({ "boundary_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "version_id": { style: "simple", explode: false } })
    .query<GetBoundaryVersionQuery>({ "minimum_boundary_revision": { style: "form", explode: true } })
    .returns(json<Portia4EFE49B1348AFC5BA81DF069FB9A0B89E1FBF2AD30994145DEF20BA105EBDEBE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listClientServices: get("/api/v1/tenants/{tenant_id}/client-services")
    .params<ListClientServicesPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListClientServicesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaAC26148312D1D19C14B10EE61B64A0F265378CB38284D4E50CE0C3D7153A7508>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getClientService: get("/api/v1/tenants/{tenant_id}/client-services/{service_id}")
    .params<GetClientServicePath>({ "service_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetClientServiceQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaED3F7B7FEBE1F20B8D98DAE3ABE7516820F289A5DFA781A7229620B1EC2C9BF7>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseClientService: put("/api/v1/tenants/{tenant_id}/client-services/{service_id}")
    .params<ReviseClientServicePath>({ "service_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireClientService: post("/api/v1/tenants/{tenant_id}/client-services/{service_id}/retirements")
    .params<RetireClientServicePath>({ "service_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listClientServiceRevisions: get("/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions")
    .params<ListClientServiceRevisionsPath>({ "service_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListClientServiceRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_service_revision": { style: "form", explode: true } })
    .returns(json<PortiaCCB8911E2FB70686013446C9D524FAAFA073B59022AB0B13FB7788B463ACDDE8>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getClientServiceRevision: get("/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}")
    .params<GetClientServiceRevisionPath>({ "revision": { style: "simple", explode: false }, "service_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia8165C4E0E3F218936EE79C52D55D60B2229FE25D669BF8BC3124D4D9E388B010>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCriteriaCatalogEditions: get("/api/v1/tenants/{tenant_id}/criteria-editions")
    .params<ListCriteriaCatalogEditionsPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia3FB59D2954C0596502DAAFC4D857D1FECF2F3D8E1E967CE98B8A0A7D8B5A8D6F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCriteriaCatalogEdition: get("/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}")
    .params<GetCriteriaCatalogEditionPath>({ "edition_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia5647C83C9923AF4B8A5A60D0E0D9CB27904F9C4571DEAE7F66B50E1FACD98453>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCriteriaCatalogEntries: get("/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries")
    .params<ListCriteriaCatalogEntriesPath>({ "edition_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCriteriaCatalogEntriesQuery>({ "category": { style: "form", explode: true }, "cursor": { style: "form", explode: true }, "kind": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "parent_identifier": { style: "form", explode: true } })
    .returns(json<PortiaDF9CF8F410540161933C37D2952A4754D8E575F091E0F87B7AFD038C972A5738>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCriteriaCatalogEntry: get("/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}")
    .params<GetCriteriaCatalogEntryPath>({ "edition_id": { style: "simple", explode: false }, "identifier": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia4FFF06F1347F0DF9A617B1AA79C57C4BE787E4DE34BC7EED6A0E8052C8F5EF7C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listDataFlows: get("/api/v1/tenants/{tenant_id}/data-flows")
    .params<ListDataFlowsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListDataFlowsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia74A5A31DE9F3A44C25116AA828DDAC667A065C414D865FF4FF6A5FC4C4C5A47A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordDataFlow: post("/api/v1/tenants/{tenant_id}/data-flows")
    .params<RecordDataFlowPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "source_type": string | null;
  "source_id": string;
  "destination_type": string | null;
  "information_asset_ids": Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
  "purpose": string | null;
  "encrypted_in_transit": boolean;
  "encrypted_at_rest": boolean;
  "effective_from": string;
  "owner_person_id": string;
  "destination_id"?: string;
  "destination_party"?: string | null;
  "exception_reference"?: string | null;
}>())
    .returns(json<Portia97032B105C2A6D6A1B226A232D4A8D42B4A96F297375BA0A5D48F0938F15A14C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getDataFlow: get("/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}")
    .params<GetDataFlowPath>({ "data_flow_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetDataFlowQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia3BD7B3C292671FD9C7BC3514F31B918E92F82B060108984374D5D9FD3FAF7737>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseDataFlow: put("/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}")
    .params<ReviseDataFlowPath>({ "data_flow_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "source_type": string | null;
  "source_id": string;
  "destination_type": string | null;
  "information_asset_ids": Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
  "purpose": string | null;
  "encrypted_in_transit": boolean;
  "encrypted_at_rest": boolean;
  "effective_from": string;
  "owner_person_id": string;
  "lifecycle": string | null;
  "destination_id"?: string;
  "destination_party"?: string | null;
  "exception_reference"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listDataFlowRevisions: get("/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}/revisions")
    .params<ListDataFlowRevisionsPath>({ "data_flow_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListDataFlowRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia74A5A31DE9F3A44C25116AA828DDAC667A065C414D865FF4FF6A5FC4C4C5A47A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listInformationAssets: get("/api/v1/tenants/{tenant_id}/information-assets")
    .params<ListInformationAssetsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListInformationAssetsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaA277510249DA33D1E77EA806415366BBFB478D775605D021833F8109CE070FCA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordInformationAsset: post("/api/v1/tenants/{tenant_id}/information-assets")
    .params<RecordInformationAssetPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
  "classification": string | null;
  "retention_reference": string | null;
  "owner_person_id": string;
  "description"?: string | null;
}>())
    .returns(json<Portia45F55D96B4D015448EC3803E9C00616C8AA2610641128A2A4A9CCB81303CEE9D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getInformationAsset: get("/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}")
    .params<GetInformationAssetPath>({ "information_asset_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetInformationAssetQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia463D989E08D87242FE062F7ABA25B4A4852CDED11C651DDB003CB1493628FBA2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseInformationAsset: put("/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}")
    .params<ReviseInformationAssetPath>({ "information_asset_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "name": string | null;
  "classification": string | null;
  "retention_reference": string | null;
  "owner_person_id": string;
  "lifecycle": string | null;
  "description"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewInformationAssetChange: post("/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews")
    .params<PreviewInformationAssetChangePath>({ "information_asset_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "classification"?: string | null;
  "lifecycle"?: string | null;
}>())
    .returns(json<Portia7447FB6BBB482B1C467364D016B654F11C2ED1628609E41058681ED0CB44705E>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listInformationAssetRevisions: get("/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/revisions")
    .params<ListInformationAssetRevisionsPath>({ "information_asset_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListInformationAssetRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaA277510249DA33D1E77EA806415366BBFB478D775605D021833F8109CE070FCA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  inviteTenantMember: post("/api/v1/tenants/{tenant_id}/invitations")
    .params<InviteTenantMemberPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "email_address": string | null;
  "affiliation": string | null;
  "administrator"?: boolean;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  acceptTenantInvitation: post("/api/v1/tenants/{tenant_id}/invitations/acceptance")
    .params<AcceptTenantInvitationPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "email_address": string | null;
  "token": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTenantInvitations: get("/api/v1/tenants/{tenant_id}/member-invitations")
    .params<ListTenantInvitationsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListTenantInvitationsQuery>({ "cursor": { style: "form", explode: true }, "email_address": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaC3C24AD50D924F9A1F4D0C270B605BEB0F1317AF1B9B674857E2967845D5EABE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  inviteOrganizationMember: post("/api/v1/tenants/{tenant_id}/member-invitations")
    .params<InviteOrganizationMemberPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "email_address": string | null;
  "built_in_role": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTenantMembers: get("/api/v1/tenants/{tenant_id}/members")
    .params<ListTenantMembersPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListTenantMembersQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaC6B58953EEC5AF0CF98AEC8E58EA963F8A1EF8B3D2BFA36F2F60E3073C7B1556>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTenantMember: get("/api/v1/tenants/{tenant_id}/members/{user_id}")
    .params<GetTenantMemberPath>({ "tenant_id": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(json<PortiaFA8797A2099AD3C0E34408B3661A645092B14759512BA3656FC05B7770BF1E26>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getMemberAccess: get("/api/v1/tenants/{tenant_id}/members/{user_id}/access")
    .params<GetMemberAccessPath>({ "tenant_id": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .query<GetMemberAccessQuery>({ "expected_built_in_role": { style: "form", explode: true } })
    .returns(json<Portia1E5BBCD6BE4B1F969208CC6975472A2A383EBE2A1300801E1AE359E9E687516A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listMemberResponsibilities: get("/api/v1/tenants/{tenant_id}/members/{user_id}/responsibilities")
    .params<ListMemberResponsibilitiesPath>({ "tenant_id": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(json<Portia96294E0FB33C6297223052ED7387F5E7FAA0C2A7CD4DC2CD6B9BFA4998A6D5C7>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  suspendMember: post("/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions")
    .params<SuspendMemberPath>({ "tenant_id": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .body(json<{
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reinstateMember: del("/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions")
    .params<ReinstateMemberPath>({ "tenant_id": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPeople: get("/api/v1/tenants/{tenant_id}/people")
    .params<ListPeoplePath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListPeopleQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaC646B2FC53C967587683B193B991EC40E30A4137E8479560E880A6AAEA504244>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordPerson: post("/api/v1/tenants/{tenant_id}/people")
    .params<RecordPersonPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "display_name": string | null;
  "work_email"?: string | null;
}>())
    .returns(json<PortiaA4299F97F2F2793A334427A6DBE3AB10187D44660E16D667981207CF14AD4341>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getPerson: get("/api/v1/tenants/{tenant_id}/people/{person_id}")
    .params<GetPersonPath>({ "person_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetPersonQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaE533A13177B62D1EFE8E47B2C8DBE1B703CC5A9D68DD56C8A5015618FD353625>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  revisePerson: put("/api/v1/tenants/{tenant_id}/people/{person_id}")
    .params<RevisePersonPath>({ "person_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "display_name": string | null;
  "work_email"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  correlatePersonMembership: put("/api/v1/tenants/{tenant_id}/people/{person_id}/membership-correlation")
    .params<CorrelatePersonMembershipPath>({ "person_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "user_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPrograms: get("/api/v1/tenants/{tenant_id}/programs")
    .params<ListProgramsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListProgramsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia1B41424DD1048776063A851A5730872FAE1FDEBEDCC12ABB187E9407CB0972F3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createProgram: post("/api/v1/tenants/{tenant_id}/programs")
    .params<CreateProgramPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
  "plan": Portia9CA30FFC559321862CA47D0242119C614DCEC0E1E9443BE7873F142DB785415F;
}>())
    .returns(json<Portia7F8B4C9203507BBF4A214CB378B625DDB003E856C25F722EAE639A78025702C1>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getProgram: get("/api/v1/tenants/{tenant_id}/programs/{program_id}")
    .params<GetProgramPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetProgramQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia6C1C6B7EB17FBC6FFD9D02B19FE9C2FA4FB3C6789E90B0635C3EE527EB8F1603>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseProgram: put("/api/v1/tenants/{tenant_id}/programs/{program_id}")
    .params<ReviseProgramPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "name": string | null;
  "plan": Portia9CA30FFC559321862CA47D0242119C614DCEC0E1E9443BE7873F142DB785415F;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listProgramBoundaries: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
    .params<ListProgramBoundariesPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListProgramBoundariesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaD775A5D8BCBA1B953633C0582AECCE997439EE8D2C3F4CB8CF37FA9D5E33A873>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createBoundary: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
    .params<CreateBoundaryPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "content": Portia1E60B56A95A64AF1BB8BE85BC8B2174D1CD473A879DA9B1370C87F09E86C448F;
}>())
    .returns(json<Portia0FBE6D4CEB2CC84764DF0FFF82C76BB333FB69E323061CA548534BC3DFB78B51>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCampaigns: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns")
    .params<ListCampaignsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCampaignsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia4F9472E8C89E75A49CAA5EB8FF7F639F8D013338A1A5DAFFE37FAD78E28C30DA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCampaign: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}")
    .params<GetCampaignPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetCampaignQuery>({ "as_of": { style: "form", explode: true } })
    .returns(json<PortiaFF553F232A8414665BAE406E202CCC0CC27AC9885754BE84D83F540E440E0733>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  acknowledgePolicy: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements")
    .params<AcknowledgePolicyPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "person_id": string;
  "policy_version": number | string;
  "content_sha256": string | null;
  "acknowledgement_text": string | null;
}>())
    .returns(json<Portia141A8449007BC3642408B5E3A41E19F30E829AC6B607080B6F410B38E370BD2E>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCampaignAmendments: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/amendments")
    .params<ListCampaignAmendmentsPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCampaignAmendmentsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaE86118FEDCAAD9C8EECB6F0820286060A99630C598D8AE761402890F6BA8A2E2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  closeCampaign: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure")
    .params<CloseCampaignPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "rationale": string | null;
}>())
    .returns(json<PortiaFF553F232A8414665BAE406E202CCC0CC27AC9885754BE84D83F540E440E0733>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordTrainingCompletion: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions")
    .params<RecordTrainingCompletionPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "person_id": string;
  "requirement_version": number | string;
  "completed_on": string;
  "source": string | null;
  "evidence_reference": string | null;
}>())
    .returns(json<PortiaE7F9DFDF55BC2B0ECD9A9FF37523ADE10CA787A7D4B97D66CCE5EAF13D29AFA4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCampaignParticipants: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/participants")
    .params<ListCampaignParticipantsPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCampaignParticipantsQuery>({ "as_of": { style: "form", explode: true }, "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "state": { style: "form", explode: true } })
    .returns(json<PortiaCAB9DEB40F4B7BA004A8D9BA3C94D8944AE60BFF46D6005CDD7D49FFE715CBA0>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reconcileCampaignAudience: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations")
    .params<ReconcileCampaignAudiencePath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "roster_snapshot_id": string;
}>())
    .returns(json<Portia30E355AC0C727AD6771307EDF8639387797EB7EF33A7FF597E35281049D3A67B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveCampaignWaiver: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers")
    .params<ApproveCampaignWaiverPath>({ "campaign_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "person_id": string;
  "reason": string | null;
  "expires_on": string;
}>())
    .returns(json<Portia68D897B5E51EC2FC3E79D0212B58DE2291C916C1893D2C59029BFF357274E581>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listProgramClientServices: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
    .params<ListProgramClientServicesPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListProgramClientServicesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaAC26148312D1D19C14B10EE61B64A0F265378CB38284D4E50CE0C3D7153A7508>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createClientService: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
    .params<CreateClientServicePath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
  "purpose": string | null;
  "owner_reference": string | null;
}>())
    .returns(json<PortiaCADDBB36C737CF14F2057C0A4E15C9E63FDD811D019E6BEFA5647B9E2BA9E2C8>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCommitmentDrafts: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts")
    .params<ListCommitmentDraftsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCommitmentDraftsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia17403E183EEEE5417F57DA6B2D792B7A88B5E631D52B05919D48C9D69FFB6C0D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createCommitmentDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts")
    .params<CreateCommitmentDraftPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "service_id": string;
  "kind": string | null;
  "identifier": string | null;
  "statement": string | null;
  "context": string | null;
  "source_reference": string | null;
}>())
    .returns(json<PortiaB40D0675C1B674F43BFBDCE25F20837F7CF28C29B2067DF1534D10FA9DE94F05>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCommitmentDraft: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}")
    .params<GetCommitmentDraftPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetCommitmentDraftQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia3AA867E8899C6C43AB80B916C3F53DC57184409D52C29C8217ABCD226DD97BB5>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseCommitmentDraft: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}")
    .params<ReviseCommitmentDraftPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "statement": string | null;
  "context": string | null;
  "source_reference": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveCommitmentDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals")
    .params<ApproveCommitmentDraftPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "effective_from": string;
  "rationale": string | null;
  "impact_digest": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCommitmentDecisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/decisions")
    .params<ListCommitmentDecisionsPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCommitmentDecisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaB5702939DCF5C30F95C9A73E21F8F0F14F920AEA657BCC2634DED63C18886EEF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEffectiveCommitmentVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/effective-version")
    .params<GetEffectiveCommitmentVersionPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetEffectiveCommitmentVersionQuery>({ "effective_on": { style: "form", explode: true } })
    .returns(json<Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewCommitmentImpact: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/impact-preview")
    .params<PreviewCommitmentImpactPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<PreviewCommitmentImpactQuery>({ "expected_revision": { style: "form", explode: true } })
    .returns(json<Portia92D234454C20DC8E7698D3F4608F463CC45108FD7C6FCB35EBDEFD7131274C0D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewCommitmentDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews")
    .params<ReviewCommitmentDraftPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "owner_reference"?: string | null;
  "applicability"?: string | null;
  "interpretation"?: string | null;
  "interpretation_note"?: string | null;
  "source_verified_reference"?: string | null;
  "source_evidence"?: string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCommitmentDraftRevisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions")
    .params<ListCommitmentDraftRevisionsPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCommitmentDraftRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_draft_revision": { style: "form", explode: true } })
    .returns(json<Portia7B41C47C738A8DA20460C8A8BE569630949FEA19EAB61C34DCBC49E83CDD123C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCommitmentDraftRevision: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions/{revision}")
    .params<GetCommitmentDraftRevisionPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "revision": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia5D9B3D88A76CFA247FEA271A78CAAC133EF00DBEBE6B5C8AC9431402F338F317>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCommitmentVersions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions")
    .params<ListCommitmentVersionsPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCommitmentVersionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia013D7F6C3C0FEB6E8082C97A2D31504B222E662A6A84DD2B56600D5BF2D16139>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCommitmentVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions/{version}")
    .params<GetCommitmentVersionPath>({ "draft_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "version": { style: "simple", explode: false } })
    .returns(json<Portia96B1731EB5D13928DBD0ED8D189660CF91F7740AD92E08EBFD63D3BF2BBC958F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlCriterionMappings: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings")
    .params<ListControlCriterionMappingsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlCriterionMappingsQuery>({ "control_id": { style: "form", explode: true }, "cursor": { style: "form", explode: true }, "edition_id": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "status": { style: "form", explode: true } })
    .returns(json<PortiaFCF00D94A0472AF13CDF0CB77F1A463EF3413B0EDADF00C16BFD3C56766F97C2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeControlCriterionMapping: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings")
    .params<ProposeControlCriterionMappingPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "control_id": string;
  "control_version_id": string;
  "edition_id": string;
  "criterion_identifier": string | null;
  "expected_revision": number | string;
  "rationale": string | null;
  "applicability_explanation": string | null;
}>())
    .returns(json<Portia274B659861C11787AC6AB4C8D76C648726966FA7DA2D30804A55E91A1C4D27B2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlCriterionMapping: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}")
    .params<GetControlCriterionMappingPath>({ "mapping_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia28D5187F005E5A41920CA5A644AAA3FD5806E031D8B7A74C154FFEB4E97A5BB3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireControlCriterionMapping: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements")
    .params<RetireControlCriterionMappingPath>({ "mapping_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewControlCriterionMapping: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews")
    .params<ReviewControlCriterionMappingPath>({ "mapping_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlDrafts: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls")
    .params<ListControlDraftsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlDraftsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaA575A714BD65E0A40C74D0ADDC7743F0FC29A5EC04226EA7371EBAD7B642251C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createControlDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls")
    .params<CreateControlDraftPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "identifier": string | null;
  "content": Portia4A57DD196CC4DFA74BA2A81FA6DD69B318AF958916620C3396D9D260326EE1B3;
}>())
    .returns(json<PortiaCE291342413ACA5E55E75E29F7C92CAA822AC0B201F64DA7A38365474F0782CD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCurrentControlVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/current-version")
    .params<GetCurrentControlVersionPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia293DE426AA53AD49D4AE678B0FEDB2F4734ED0C23AA4984147FA026082080856>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlDecisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions")
    .params<ListControlDecisionsPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlDecisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaC39C98E2DBB3BF252193560530969F5DF01D0CB33395E2B40E2B8496665ED57A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlDecision: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions/{decision_id}")
    .params<GetControlDecisionPath>({ "control_id": { style: "simple", explode: false }, "decision_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia9B0607CA8CEF3829E2071107C5E2DC5C50730AFFBF2290344613FA16D3D34583>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlDraft: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
    .params<GetControlDraftPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetControlDraftQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia88689C12C0FB6431DB31439CCE47EDDF15E892678804B4AAC1633CF025902FA4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseControlDraft: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
    .params<ReviseControlDraftPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "content": Portia4A57DD196CC4DFA74BA2A81FA6DD69B318AF958916620C3396D9D260326EE1B3;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveControl: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals")
    .params<ApproveControlPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "effective_from": string;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
  "impact_digest"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  discardControlDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards")
    .params<DiscardControlDraftPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  designateControlOwnerPerson: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person")
    .params<DesignateControlOwnerPersonPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "person_id"?: string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewControl: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews")
    .params<ReviewControlPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlDraftRevisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions")
    .params<ListControlDraftRevisionsPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlDraftRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_control_draft_revision": { style: "form", explode: true } })
    .returns(json<Portia47859CF5FF8AFC9721EEF5A600985A572F055CA4FA57B5D4FB578E7C08549EC9>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlDraftRevision: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}")
    .params<GetControlDraftRevisionPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "revision": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia46334DBC52E6A1AB64DEA808B9B7518BD8CA5116F786F7BAAF173857E10A75C3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEffectiveControlVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/effective-version")
    .params<GetEffectiveControlVersionPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetEffectiveControlVersionQuery>({ "effective_on": { style: "form", explode: true } })
    .returns(json<Portia293DE426AA53AD49D4AE678B0FEDB2F4734ED0C23AA4984147FA026082080856>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlEvaluations: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations")
    .params<ListControlEvaluationsPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlEvaluationsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "state": { style: "form", explode: true } })
    .returns(json<Portia64968A1E13B54F46E6D59363F33BAE3CC4FF6500FD184206E0B3BFBEA3161514>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  startControlEvaluation: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations")
    .params<StartControlEvaluationPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "steps"?: PortiaA25FBFD25C5DA718EF703EC9807E1F0349D507CDB2F63B9398B7F26E9EC369D9;
  "retest_of_evaluation_id"?: string;
}>())
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlEvaluation: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}")
    .params<GetControlEvaluationPath>({ "control_id": { style: "simple", explode: false }, "evaluation_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  disposeControlEvaluationDeviation: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions")
    .params<DisposeControlEvaluationDeviationPath>({ "control_id": { style: "simple", explode: false }, "deviation_id": { style: "simple", explode: false }, "evaluation_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "disposition": string | null;
  "rationale": string | null;
  "waiver_id"?: string;
}>())
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewControlEvaluation: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews")
    .params<ReviewControlEvaluationPath>({ "control_id": { style: "simple", explode: false }, "evaluation_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "decision": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordControlEvaluationStep: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results")
    .params<RecordControlEvaluationStepPath>({ "control_id": { style: "simple", explode: false }, "evaluation_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "step_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "result": string | null;
  "rationale": string | null;
  "inspected_items": PortiaD903C9F6E761AA93918B6873BE31012F4F4BD0B196CC911D5AE8BD35E6B778B0;
  "deviation_classification"?: string | null;
  "deviation_description"?: string | null;
}>())
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  submitControlEvaluation: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions")
    .params<SubmitControlEvaluationPath>({ "control_id": { style: "simple", explode: false }, "evaluation_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "conclusions": PortiaB47D44CD61F1BAE17F9B3642AC00CEF06F2C4D1DEB0364DFC7C6B68A7F1C6300;
}>())
    .returns(json<Portia83D07B4EFEB18ECE4EF3A7B4B6F2244853EEB9FFDBEFBF7F04C3C0CEAE56B9CB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewControlImpact: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/impact-preview")
    .params<PreviewControlImpactPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<PreviewControlImpactQuery>({ "expected_revision": { style: "form", explode: true } })
    .returns(json<Portia5C08B534C036029E349B3D0CE114F3E24CAD136A7E31207CE6E7D5B26A094755>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlOccurrences: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences")
    .params<ListControlOccurrencesPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlOccurrencesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "state": { style: "form", explode: true } })
    .returns(json<Portia613EC1E24171579C38DBADD1656E2EBACEFD2C2F65F7AE93AAB5427AD81797DE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  openControlOccurrence: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences")
    .params<OpenControlOccurrencePath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "trigger": string | null;
  "occurred_on": string;
}>())
    .returns(json<PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlOccurrence: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}")
    .params<GetControlOccurrencePath>({ "control_id": { style: "simple", explode: false }, "occurrence_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  attestControlOccurrence: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations")
    .params<AttestControlOccurrencePath>({ "control_id": { style: "simple", explode: false }, "occurrence_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "result": string | null;
  "performed_at": string;
  "covered_from"?: string;
  "covered_until"?: string;
  "notes"?: string | null;
  "rationale"?: string | null;
  "evidence": PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27;
  "performed_by_person_id"?: string;
}>())
    .returns(json<PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  correctControlAttestation: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections")
    .params<CorrectControlAttestationPath>({ "control_id": { style: "simple", explode: false }, "occurrence_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "result": string | null;
  "performed_at": string;
  "covered_from"?: string;
  "covered_until"?: string;
  "notes"?: string | null;
  "rationale"?: string | null;
  "evidence": PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27;
  "correction_reason": string | null;
  "performed_by_person_id"?: string;
}>())
    .returns(json<PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewControlOccurrence: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews")
    .params<ReviewControlOccurrencePath>({ "control_id": { style: "simple", explode: false }, "occurrence_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "attestation_id": string;
  "outcome": string | null;
  "rationale": string | null;
  "requested_actions"?: PortiaEBEBAE40CDBFE8B4726C2E5FDB6EE27438942900064BA238E5F7452B92135107;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<PortiaF04DF7A5E293A53D9AB5D519AC3C499DF057DE3B1C4CFD202ABDCB850A627B54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlOperatingPlan: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan")
    .params<GetControlOperatingPlanPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia9065D279549CE38EF784C0EDC609B94880345498852BE7465233B097BA2F7869>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewControlOperatingPlan: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/preview")
    .params<PreviewControlOperatingPlanPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "control_version_id": string;
  "owner": Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7;
  "backup_owner"?: Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7;
  "reviewer_member_id": string;
  "cadence": Portia98DBC88EDB4D5BDDE18F31C1436D60500C23C1F99805D1913C5915F5ADDC59DD;
  "effective_from": string;
}>())
    .returns(json<Portia9EE7CFBC2DB1E57A7AB5450792B4056ACF5FDF2A4383256FD21D6EEEBF893879>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeControlOperatingPlan: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals")
    .params<ProposeControlOperatingPlanPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "control_version_id": string;
  "owner": Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7;
  "backup_owner"?: Portia0FAA5D97CF311C7291EA6ADDA14F5C608F6E845037404DCF66A7FA2DA33BEAF7;
  "reviewer_member_id": string;
  "cadence": Portia98DBC88EDB4D5BDDE18F31C1436D60500C23C1F99805D1913C5915F5ADDC59DD;
  "effective_from": string;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<PortiaFDFB5EB07BD18FCE44E06DBD37F46C64A2C4C2935EAE7D0785664C275FB1204A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveControlOperatingPlan: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals")
    .params<ApproveControlOperatingPlanPath>({ "control_id": { style: "simple", explode: false }, "plan_version_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<PortiaFDFB5EB07BD18FCE44E06DBD37F46C64A2C4C2935EAE7D0785664C275FB1204A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  withdrawControlProposal: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals")
    .params<WithdrawControlProposalPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeControlRetirement: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals")
    .params<ProposeControlRetirementPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_approved_version_id": string;
  "effective_until": string;
  "rationale": string | null;
}>())
    .returns(json<PortiaF3BACE104C01FC9709E1FE05FA74F9ED4D50EA2EACF73C77510A4B743F9B8CC6>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireControl: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements")
    .params<RetireControlPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "impact_digest": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeControlSuccessor: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors")
    .params<ProposeControlSuccessorPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_approved_version_id": string;
  "content": Portia4A57DD196CC4DFA74BA2A81FA6DD69B318AF958916620C3396D9D260326EE1B3;
}>())
    .returns(json<Portia5C785825C5C3F7E845CCBEE2767058870CB823F13882AECF91DBC1CBDA9F87FC>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlVersions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions")
    .params<ListControlVersionsPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlVersionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaB2401AF589C5863F4C683B88251FE1B9E91B82E95AFAA57501CD8611540CBC1B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getControlVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions/{version_id}")
    .params<GetControlVersionPath>({ "control_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "version_id": { style: "simple", explode: false } })
    .returns(json<Portia293DE426AA53AD49D4AE678B0FEDB2F4734ED0C23AA4984147FA026082080856>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCriteriaCoverage: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-coverage")
    .params<ListCriteriaCoveragePath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCriteriaCoverageQuery>({ "category": { style: "form", explode: true }, "coverage_state": { style: "form", explode: true }, "cursor": { style: "form", explode: true }, "edition_id": { style: "form", explode: true }, "kind": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia4D86519FCC16F02548C3E29E580EC003867A07CF26D256F57E4B3D9A40816FFC>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  selectProgramCriteriaEdition: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-edition")
    .params<SelectProgramCriteriaEditionPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "edition_id": string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listCriterionApplicability: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability")
    .params<ListCriterionApplicabilityPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListCriterionApplicabilityQuery>({ "cursor": { style: "form", explode: true }, "edition_id": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "status": { style: "form", explode: true } })
    .returns(json<Portia9057974446C97EEBDB9DE36F30C6DB793B5538A21235EA3265B9649504F306BE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeCriterionNotApplicable: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability")
    .params<ProposeCriterionNotApplicablePath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "edition_id": string;
  "criterion_identifier": string | null;
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(json<Portia119B1B96AE3CD15CA98EA1845F142D63F8BCAC26FF8D09C7414F2C69563FF5B0>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getCriterionApplicability: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}")
    .params<GetCriterionApplicabilityPath>({ "decision_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaACD105735FFCA90DB185696163BF28386D9855A759F2E53F1B2B4A58AFE087CD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewCriterionApplicability: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews")
    .params<ReviewCriterionApplicabilityPath>({ "decision_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  withdrawCriterionNotApplicable: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals")
    .params<WithdrawCriterionNotApplicablePath>({ "decision_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listFindings: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings")
    .params<ListFindingsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListFindingsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "readiness_status": { style: "form", explode: true } })
    .returns(json<Portia717F77E4665D9616BE94EEEF72BC01EF932245408F2AEAE20394ADACE6A23687>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  raiseFinding: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings")
    .params<RaiseFindingPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "source": Portia1B51602F06141206ED19B855080CC6A10F95F1354F62A54606D6649DA73FBC07;
  "title": string | null;
  "description": string | null;
  "severity": string | null;
  "affected_scope": string | null;
  "owner_member_id": string;
  "due_on": string;
  "links"?: Portia871F8CC05AD7D5F92C62179C2FAF4B0C9482F621AC1C5337DA051CA3449935F8;
}>())
    .returns(json<Portia1AABC072058538BE7FD1B099EC8BCE55A4F7A5AAF4C4B650FF18FB1F414E1E86>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getFinding: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}")
    .params<GetFindingPath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  linkFindingAcceptance: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances")
    .params<LinkFindingAcceptancePath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "kind": string | null;
  "record_id": string;
  "decision_id"?: string;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  closeFinding: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures")
    .params<CloseFindingPath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "verification_rationale": string | null;
  "resolution_evidence": PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  addCorrectiveAction: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions")
    .params<AddCorrectiveActionPath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "description": string | null;
  "owner_member_id": string;
  "due_on": string;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  completeCorrectiveAction: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions")
    .params<CompleteCorrectiveActionPath>({ "action_id": { style: "simple", explode: false }, "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "resolution_notes": string | null;
  "evidence": PortiaABC62954E80A62897E82DF675C5392B33BE1B4C71C1B877E8115AC0CBFD97E27;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reopenFinding: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings")
    .params<ReopenFindingPath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "reason": string | null;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseFinding: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions")
    .params<ReviseFindingPath>({ "finding_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "severity": string | null;
  "owner_member_id": string;
  "due_on": string;
  "affected_scope": string | null;
  "root_cause"?: string | null;
  "reason": string | null;
}>())
    .returns(json<PortiaCBA6630CFA91012BCB9C1FDEAAE964104EC054B36A1E0C6FE396D235D74C8C4F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listMyControlWork: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/my-work")
    .params<ListMyControlWorkPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListMyControlWorkQuery>({ "horizon_days": { style: "form", explode: true } })
    .returns(json<PortiaE9B2575F51D37D152BB376D660B9FD59648CDADCCFB631542854BC97BC5CBA7D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listControlOperatingBlockers: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/operating-blockers")
    .params<ListControlOperatingBlockersPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListControlOperatingBlockersQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia183272C2785AB7DAF0DDB9887649BB6524E07D26A9750A0F1422C8AD50831FB9>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPolicies: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies")
    .params<ListPoliciesPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListPoliciesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia2664E66364A3BD18497EAE1B5A14F44FE6851C2CFF87AA850690EA322CF1731E>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createPolicyDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies")
    .params<CreatePolicyDraftPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "identifier": string | null;
  "content": Portia0F54B0A48070763F53BE45B0F025C1745F6F2EE7C0D73202A16DA9FE1E41E536;
}>())
    .returns(json<Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getPolicy: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}")
    .params<GetPolicyPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia223D8ED908741464C092218194160F10C7407F5339064207F7EDDFB8FC5706D2>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approvePolicy: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/approvals")
    .params<ApprovePolicyPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "effective_from": string;
  "major": boolean;
  "rationale": string | null;
  "impact_digest"?: string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia9FA5D5CF525C4AF42E939440C7466EDE599F60FC5B4D7162FD53DD072DBF2027>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPolicyDecisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/decisions")
    .params<ListPolicyDecisionsPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListPolicyDecisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia4DD7D3410340405FBA257F32935EE3C1C844A4AB7348761CE6EC36FE0442CFEA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  revisePolicyDraft: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft")
    .params<RevisePolicyDraftPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "content": Portia0F54B0A48070763F53BE45B0F025C1745F6F2EE7C0D73202A16DA9FE1E41E536;
}>())
    .returns(json<Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  discardPolicyDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft/discards")
    .params<DiscardPolicyDraftPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEffectivePolicyVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/effective-version")
    .params<GetEffectivePolicyVersionPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetEffectivePolicyVersionQuery>({ "effective_on": { style: "form", explode: true } })
    .returns(json<Portia9FA5D5CF525C4AF42E939440C7466EDE599F60FC5B4D7162FD53DD072DBF2027>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewPolicyImpact: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/impact-preview")
    .params<PreviewPolicyImpactPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<PreviewPolicyImpactQuery>({ "expected_revision": { style: "form", explode: true } })
    .returns(json<PortiaF81CA4F12726F70D1A19DD820214E8112F0BB998595F2B4B96ACC142CEDA1289>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  confirmPolicyReview: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/periodic-reviews")
    .params<ConfirmPolicyReviewPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_version": number | string;
  "rationale": string | null;
}>())
    .returns(json<Portia6C9CF32EA0FA52D3E46677C05C4B09117A3F07A53F1D7BF0E3FD06FA6B3248BB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposePolicyRetirement: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirement-proposals")
    .params<ProposePolicyRetirementPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_version": number | string;
  "effective_until": string;
  "rationale": string | null;
}>())
    .returns(json<Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approvePolicyRetirement: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirements")
    .params<ApprovePolicyRetirementPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "accepted_review_decision_id": string;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia6C9CF32EA0FA52D3E46677C05C4B09117A3F07A53F1D7BF0E3FD06FA6B3248BB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewPolicyDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/reviews")
    .params<ReviewPolicyDraftPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia6C9CF32EA0FA52D3E46677C05C4B09117A3F07A53F1D7BF0E3FD06FA6B3248BB>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposePolicySuccessor: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/successors")
    .params<ProposePolicySuccessorPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_version": number | string;
  "content": Portia0F54B0A48070763F53BE45B0F025C1745F6F2EE7C0D73202A16DA9FE1E41E536;
}>())
    .returns(json<Portia848218C1EDDB0BB69AE69A522A5399F4F051FF727DB3D3A8516B5D470E862F9C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listPolicyVersions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions")
    .params<ListPolicyVersionsPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListPolicyVersionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia8A10B3D9610CE4B221E33CC56E464200ACA1EE75D7D9944E4FE43F40B69577C1>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getPolicyVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions/{version}")
    .params<GetPolicyVersionPath>({ "policy_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "version": { style: "simple", explode: false } })
    .returns(json<Portia9FA5D5CF525C4AF42E939440C7466EDE599F60FC5B4D7162FD53DD072DBF2027>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  launchPolicyCampaign: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns")
    .params<LaunchPolicyCampaignPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "policy_id": string;
  "policy_version": number | string;
  "roster_snapshot_id": string;
  "due_on": string;
  "instructions"?: string | null;
}>())
    .returns(json<Portia51FE555DD79808725AEE8E82914094E523850FDA6EFBE755215A4071F1A05B52>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listReadinessAssessments: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments")
    .params<ListReadinessAssessmentsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListReadinessAssessmentsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia38AC222880FC3D76BC0113586E6EADD6FC945BF8A6AE17809DC976132058D642>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  runReadinessAssessment: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments")
    .params<RunReadinessAssessmentPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "as_of"?: string;
}>())
    .returns(json<PortiaDE57F78A9D37368116B1A7EC1CA79CF96CE81F200951380435CC4A669F8837DE>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getReadinessAssessment: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}")
    .params<GetReadinessAssessmentPath>({ "assessment_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaC95DF1FEF74AB78C247A61B1C7CA71C04A8A85C72948A02B997E3FFA2295B1A6>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listReadinessAnnotations: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/annotations")
    .params<ListReadinessAnnotationsPath>({ "assessment_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListReadinessAnnotationsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia687EE1C23460870912691E4FB708323EBC207241ED63637488F412D5496EE5EA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  decideReadiness: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision")
    .params<DecideReadinessPath>({ "assessment_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia29DA1B59E17FFC5BC5537FA36443BD20DD5A6C59FED0530C3D8E91FBB7EAB977>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listReadinessGaps: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps")
    .params<ListReadinessGapsPath>({ "assessment_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListReadinessGapsQuery>({ "cursor": { style: "form", explode: true }, "kind": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "owner_member_id": { style: "form", explode: true }, "plan_state": { style: "form", explode: true }, "rule_id": { style: "form", explode: true }, "subject": { style: "form", explode: true } })
    .returns(json<PortiaF9B77C7ADBEF0EB8FE1C2659269AA790D9F0D90014FCAFB7B2571FB108D35FD8>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  annotateReadinessGap: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations")
    .params<AnnotateReadinessGapPath>({ "assessment_id": { style: "simple", explode: false }, "gap_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "body": string | null;
}>())
    .returns(json<Portia50C70F44CE8894CF7231272888D6062DE18FA19BF04DAC0834A26B9130C165A1>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  decideTypeIEntry: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision")
    .params<DecideTypeIEntryPath>({ "assessment_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "acknowledged_gap_ids"?: Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia7A69D33280F826A4E8D05D28C507E4C68556897E2948CDFB8FF09D153ED9DF44>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  planReadinessGap: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan")
    .params<PlanReadinessGapPath>({ "gap_id": { style: "simple", explode: false }, "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "owner_member_id": string;
  "target_date": string;
  "action": string | null;
}>())
    .returns(json<Portia24AADB452F032CF586816A93E0006D88CF6B74A01ECC66A7DD7E222A5F991AAF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTypeIEntryDecisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/type-i-entry-decisions")
    .params<ListTypeIEntryDecisionsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListTypeIEntryDecisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia29FC8CE420AC2814A94627199085A8D4740B2FCFF2200FDDEF4D0FCFD528B72D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listProgramRevisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions")
    .params<ListProgramRevisionsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListProgramRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_program_revision": { style: "form", explode: true } })
    .returns(json<Portia559C6B2CB453C53C337F5E8AA18127BB374E6308F310A0A099A30C3421B412A3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getProgramRevision: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}")
    .params<GetProgramRevisionPath>({ "program_id": { style: "simple", explode: false }, "revision": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia0B6601B102C5751CB9E0454A861D244D753DE3A3F0BA4871D392E5D08B917A5F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskMethod: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method")
    .params<GetRiskMethodPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia1DAABC0EE92F473717483C90B8CF7E689206159947287755819C2AE6D3D4ECF4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  publishRiskMethodVersion: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions")
    .params<PublishRiskMethodVersionPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_version": number | string;
  "likelihood_scale": PortiaEBEBAE40CDBFE8B4726C2E5FDB6EE27438942900064BA238E5F7452B92135107;
  "impact_scale": PortiaEBEBAE40CDBFE8B4726C2E5FDB6EE27438942900064BA238E5F7452B92135107;
  "appetite_threshold"?: number | string;
}>())
    .returns(json<Portia1DAABC0EE92F473717483C90B8CF7E689206159947287755819C2AE6D3D4ECF4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskMethodVersion: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions/{version}")
    .params<GetRiskMethodVersionPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "version": { style: "simple", explode: false } })
    .returns(json<Portia1DAABC0EE92F473717483C90B8CF7E689206159947287755819C2AE6D3D4ECF4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRiskDrafts: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks")
    .params<ListRiskDraftsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListRiskDraftsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia069969C2489ADD7162ECE80D4C8B7385BA13F5931CCE303868141921126F6CBF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  createRiskDraft: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks")
    .params<CreateRiskDraftPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "identifier": string | null;
  "title": string | null;
  "scenario": string | null;
  "potential_effect": string | null;
  "source_note"?: string | null;
}>())
    .returns(json<PortiaB5B4C6C1CCCDD997C7F390CEAD0680DD868403FA1B09E115245EBB04DFD8F886>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  acceptRisk: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances")
    .params<AcceptRiskPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "residual_assessment_id": string;
  "approver_authority": string | null;
  "expires_at": string;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia8EC39D203E40F38A63A668A3300716B942ECA6885268F24789B7087CBB825751>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordRiskAssessment: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments")
    .params<RecordRiskAssessmentPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "method_version": number | string;
  "phase": string | null;
  "likelihood": number | string;
  "impact": number | string;
  "rationale": string | null;
}>())
    .returns(json<Portia097249E53E54CC03E10877C906EFC2594FFE7AAC91C87D347D57D7145C1CC24C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  proposeRiskControlTreatment: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments")
    .params<ProposeRiskControlTreatmentPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "control_id": string;
  "control_version_id": string;
  "rationale": string | null;
}>())
    .returns(json<PortiaF38F4E93C39265245C5ADAB3FA4776FF096BEF74C5FFABFD2BC49604E5DD0416>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  retireRiskControlTreatment: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements")
    .params<RetireRiskControlTreatmentPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "treatment_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviewRiskControlTreatment: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews")
    .params<ReviewRiskControlTreatmentPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "treatment_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "outcome": string | null;
  "rationale": string | null;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskDraft: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft")
    .params<GetRiskDraftPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetRiskDraftQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaF5D86DD63563039EDAF243CA29FEAE85382ED90210D8D752F40BC3E24D4ED319>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseRiskDraft: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft")
    .params<ReviseRiskDraftPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "title": string | null;
  "scenario": string | null;
  "potential_effect": string | null;
  "source_note"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRiskDraftRevisions: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions")
    .params<ListRiskDraftRevisionsPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListRiskDraftRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_risk_revision": { style: "form", explode: true } })
    .returns(json<PortiaBE1636B313652DA76C47508EB54DDB2BC0EB3E2F94DD70683296AF4B4AC9B979>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskDraftRevision: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions/{revision}")
    .params<GetRiskDraftRevisionPath>({ "program_id": { style: "simple", explode: false }, "revision": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia3C70E8DCFE6903F2FDCA0DF299E0A8C4BE8585E75798B4037723C1A3360251E4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskEvaluation: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation")
    .params<GetRiskEvaluationPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetRiskEvaluationQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaAB78B394BEAB9C3EAC62EC80E078A79A64B1AE85D3A626BAD90D554F0E0FF5F8>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRiskEvaluationHistory: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation/history")
    .params<ListRiskEvaluationHistoryPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListRiskEvaluationHistoryQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaAF9D90F9D29976C8CE67E2F682A350FB57A5B5B729FF40887ACC33E345E34BBC>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRiskGovernance: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/governance")
    .params<GetRiskGovernancePath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia88B0F753F42B585D52BCAE1FC7E825AE67D21A190630E1D80A18A96727D1186F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignRiskOwner: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/owner")
    .params<AssignRiskOwnerPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "person_id": string;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  chooseRiskTreatment: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment")
    .params<ChooseRiskTreatmentPath>({ "program_id": { style: "simple", explode: false }, "risk_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "kind": string | null;
  "rationale": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listProgramSnapshots: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/scope-snapshots")
    .params<ListProgramSnapshotsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListProgramSnapshotsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia757E16DD9071C1EA6CD19C09CEF1B1E1BC16F3C168A35CA627AC8EFC9996C48D>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getProgramSetupWork: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work")
    .params<GetProgramSetupWorkPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetProgramSetupWorkQuery>({ "boundary_cursor": { style: "form", explode: true }, "boundary_id": { style: "form", explode: true }, "boundary_limit": { style: "form", explode: true }, "minimum_boundary_revision": { style: "form", explode: true }, "minimum_program_revision": { style: "form", explode: true } })
    .returns(json<Portia5AF07B3208EB9275DF00078FDEF65642C66304D924A78125731567AACD08E2A4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  launchTrainingCampaign: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns")
    .params<LaunchTrainingCampaignPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "requirement_id": string;
  "requirement_version": number | string;
  "roster_snapshot_id": string;
  "due_on": string;
  "instructions"?: string | null;
}>())
    .returns(json<Portia51FE555DD79808725AEE8E82914094E523850FDA6EFBE755215A4071F1A05B52>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTrainingRequirements: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements")
    .params<ListTrainingRequirementsPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListTrainingRequirementsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia47FBF2CE655A0BFE041F48054259143EC9BF203612F24E82B9C8E3DC73E0056B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  defineTrainingRequirement: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements")
    .params<DefineTrainingRequirementPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "identifier": string | null;
  "content": PortiaD6BB547EF6FB03ADDD6888D292BD58FA5D2D19E4D01A028E872206CC71819530;
}>())
    .returns(json<PortiaADD2B4C414E1944EE190450BDA58B841C185CE857DDB2F46E1785464B36D095B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTrainingRequirement: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}")
    .params<GetTrainingRequirementPath>({ "program_id": { style: "simple", explode: false }, "requirement_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetTrainingRequirementQuery>({ "version": { style: "form", explode: true } })
    .returns(json<Portia9D06184903CEE42AA204A04AF68AF0059EF7DEDC923932BC599AB3BB26412113>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseTrainingRequirement: put("/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}")
    .params<ReviseTrainingRequirementPath>({ "program_id": { style: "simple", explode: false }, "requirement_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_version": number | string;
  "content": PortiaD6BB547EF6FB03ADDD6888D292BD58FA5D2D19E4D01A028E872206CC71819530;
}>())
    .returns(json<PortiaADD2B4C414E1944EE190450BDA58B841C185CE857DDB2F46E1785464B36D095B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWork: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/work")
    .params<ListWorkPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListWorkQuery>({ "horizon_days": { style: "form", explode: true }, "scope": { style: "form", explode: true } })
    .returns(json<Portia895E347267064464C7EEA3101D9897828DE9ACE40AE8D3EB6B31BD0E2E552E59>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkDigest: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/digest")
    .params<GetWorkDigestPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia452F41728C6AFA9916F07B977123DCCF98284CE3451ABA0C4C76C709D48F8747>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWorkReminders: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/reminders")
    .params<ListWorkRemindersPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaE743D385FF26935D87C27AF6254C90A88FE79C15319CDD62451780019AF8EDBD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkItem: get("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}")
    .params<GetWorkItemPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "work_item_id": { style: "simple", explode: false } })
    .returns(json<Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignWorkItem: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments")
    .params<AssignWorkItemPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "work_item_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "assignee_member_id": string;
  "reason"?: string | null;
}>())
    .returns(json<Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  claimWorkItem: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims")
    .params<ClaimWorkItemPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "work_item_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
}>())
    .returns(json<Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  delegateWorkItem: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations")
    .params<DelegateWorkItemPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "work_item_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "assignee_member_id": string;
  "reason": string | null;
}>())
    .returns(json<Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  escalateWorkItem: post("/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations")
    .params<EscalateWorkItemPath>({ "program_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false }, "work_item_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "reason": string | null;
}>())
    .returns(json<Portia380ED2C37CEF989571B9646AABA84F263F4332B0073F69A22C6E3A516CEB2197>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listResponsibilities: get("/api/v1/tenants/{tenant_id}/responsibilities")
    .params<ListResponsibilitiesPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListResponsibilitiesQuery>({ "minimum_revision": { style: "form", explode: true }, "record_id": { style: "form", explode: true }, "record_type": { style: "form", explode: true }, "scope_revision": { style: "form", explode: true }, "version_id": { style: "form", explode: true } })
    .returns(json<PortiaACB074BCE785D34D94E14D208E190145EA345C44039DCEA40D390A0B5B303A9C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignResponsibility: post("/api/v1/tenants/{tenant_id}/responsibilities")
    .params<AssignResponsibilityPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "member_user_id": string;
  "type": "control_owner" | "evidence_contributor" | "assigned_reviewer" | "access_reviewer" | "corrective_action_owner" | "policy_approver";
  "record_type": string | null;
  "record_id": string;
  "version_id": string;
  "scope_revision": number | string;
  "effective_from": string;
  "effective_until"?: string;
  "separation_of_duties_waiver_ids": Portia0B8DD226C90BCDBE54774D138909CEACAC410368E2CA21791FD1B5EC3FCB4C2D;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  previewResponsibilityConflicts: get("/api/v1/tenants/{tenant_id}/responsibilities/conflict-preview")
    .params<PreviewResponsibilityConflictsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<PreviewResponsibilityConflictsQuery>({ "effective_from": { style: "form", explode: true }, "effective_until": { style: "form", explode: true }, "member_user_id": { style: "form", explode: true }, "record_id": { style: "form", explode: true }, "record_type": { style: "form", explode: true }, "scope_revision": { style: "form", explode: true }, "type": { style: "form", explode: true }, "version_id": { style: "form", explode: true } })
    .returns(json<Portia1667971800402A6979C26E522A3DC756AAF0573767E53910C942723A6B2DC3D5>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  revokeResponsibility: post("/api/v1/tenants/{tenant_id}/responsibilities/{assignment_id}/revocation")
    .params<RevokeResponsibilityPath>({ "assignment_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "record_type": string | null;
  "record_id": string;
  "version_id": string;
  "scope_revision": number | string;
  "reason": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRoles: get("/api/v1/tenants/{tenant_id}/roles")
    .params<ListRolesPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListRolesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<Portia411B3239057A525C3562E4BE0D1A71F30A3844A0A56231069471136CDD4A37E4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getRole: get("/api/v1/tenants/{tenant_id}/roles/{role_id}")
    .params<GetRolePath>({ "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia444702261363EF785C20C78504EBD948CC2950CA076C0FEA93FBD650F83E4680>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  defineRole: post("/api/v1/tenants/{tenant_id}/roles/{role_id}")
    .params<DefineRolePath>({ "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  deleteRole: del("/api/v1/tenants/{tenant_id}/roles/{role_id}")
    .params<DeleteRolePath>({ "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRolePermissions: get("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions")
    .params<ListRolePermissionsPath>({ "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListRolePermissionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<PortiaEC8A68FE610BE733B5FB5E10D0C0A79743573CEEBB9114A5BBD9B4542987EC3B>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignRolePermission: post("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}")
    .params<AssignRolePermissionPath>({ "permission": { style: "simple", explode: false }, "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  removeRolePermission: del("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}")
    .params<RemoveRolePermissionPath>({ "permission": { style: "simple", explode: false }, "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listRoleTeams: get("/api/v1/tenants/{tenant_id}/roles/{role_id}/teams")
    .params<ListRoleTeamsPath>({ "role_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListRoleTeamsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<Portia445823D45DEBC21876B8F7D21A7706F96E8DB18D68A39E931F58C101C313F09E>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  freezeProgramScopeSnapshot: post("/api/v1/tenants/{tenant_id}/scope-snapshots")
    .params<FreezeProgramScopeSnapshotPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "program_id": string;
  "expected_program_revision": number | string;
  "boundary_id": string;
  "approved_boundary_version_id": string;
}>())
    .returns(json<PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getSnapshot: get("/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}")
    .params<GetSnapshotPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetSnapshotQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaE2F70B587A599F2A92FE1BC5C9941D3194A8ABA84334EB2D1BFDB23F21AF2C00>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  amendProgramScopeSnapshot: post("/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments")
    .params<AmendProgramScopeSnapshotPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "program_id": string;
  "expected_program_revision": number | string;
  "boundary_id": string;
  "approved_boundary_version_id": string;
  "reason": string | null;
}>())
    .returns(json<PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  regenerateProgramScopeSnapshotManifest: get("/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration")
    .params<RegenerateProgramScopeSnapshotManifestPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia293AC99442386DA2E4203C76C9DCACD9B0159C4FF4F351D4C5C91AF91E5AADA5>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  verifyProgramScopeSnapshot: get("/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/verification")
    .params<VerifyProgramScopeSnapshotPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia9890D4629429B264BE737520EC815DFE7400C9A6ECC4B4451A28AF5E22133782>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordSeparationOfDutiesWaiver: post("/api/v1/tenants/{tenant_id}/separation-of-duties-waivers")
    .params<RecordSeparationOfDutiesWaiverPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "scope": Portia6EDEFDF3147D5333AAABFE7768FCC99D88F93C8F7C81708F0C551ABD6FD7A117;
  "beneficiary_user_id": string;
  "rationale": string | null;
  "expires_at": string;
}>())
    .returns(json<Portia3F6E09D9310B082917FB4E979E225D9A959D6DE8796E3B5D244A47C587B91F82>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getSeparationOfDutiesWaiver: get("/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}")
    .params<GetSeparationOfDutiesWaiverPath>({ "tenant_id": { style: "simple", explode: false }, "waiver_id": { style: "simple", explode: false } })
    .returns(json<Portia3F6E09D9310B082917FB4E979E225D9A959D6DE8796E3B5D244A47C587B91F82>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveSeparationOfDutiesWaiver: post("/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals")
    .params<ApproveSeparationOfDutiesWaiverPath>({ "tenant_id": { style: "simple", explode: false }, "waiver_id": { style: "simple", explode: false } })
    .returns(json<Portia3F6E09D9310B082917FB4E979E225D9A959D6DE8796E3B5D244A47C587B91F82>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listServiceIdentities: get("/api/v1/tenants/{tenant_id}/service-identities")
    .params<ListServiceIdentitiesPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListServiceIdentitiesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "unowned_only": { style: "form", explode: true } })
    .returns(json<PortiaDAC5690D56A8404A50FAA955F800B7A921C23C5147D404EFC243B5021316A557>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordServiceIdentity: post("/api/v1/tenants/{tenant_id}/service-identities")
    .params<RecordServiceIdentityPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "display_name": string | null;
  "identity_kind": string | null;
  "purpose": string | null;
  "owner_kind": string | null;
  "owner_id": string;
  "review_by": string;
  "environment"?: string | null;
  "expires_on"?: string;
}>())
    .returns(json<PortiaAD20397E4469C88F974BC9D5A386CD8E3C4A25CB034E5C0233899BA467B9C283>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getServiceIdentity: get("/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}")
    .params<GetServiceIdentityPath>({ "service_identity_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetServiceIdentityQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia5D4504657CF0F248BBB0D39DEB82554EBDBEFE27BFFEFBB869B53903B87F98DC>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseServiceIdentity: put("/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}")
    .params<ReviseServiceIdentityPath>({ "service_identity_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "display_name": string | null;
  "identity_kind": string | null;
  "purpose": string | null;
  "owner_kind": string | null;
  "owner_id": string;
  "review_by": string;
  "lifecycle_status": string | null;
  "environment"?: string | null;
  "expires_on"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  changeTenantSlug: post("/api/v1/tenants/{tenant_id}/slug-changes")
    .params<ChangeTenantSlugPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "slug": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  suspendTenant: post("/api/v1/tenants/{tenant_id}/suspensions")
    .params<SuspendTenantPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reactivateTenant: del("/api/v1/tenants/{tenant_id}/suspensions")
    .params<ReactivateTenantPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessExpectations: get("/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations")
    .params<ListAccessExpectationsPath>({ "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia055CA1599B22C4A39EEDF56A03F53F231CEBE6075D8937F744977DD9D66E54FD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  approveAccessExpectation: post("/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval")
    .params<ApproveAccessExpectationPath>({ "expectation_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_ledger_revision": number | string;
  "separation_of_duties_waiver_id"?: string;
}>())
    .returns(json<Portia1FF79BD41EDC10E67AF82C5AC8AA375E30EB20870D88CD7322218F9CD2983831>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  exemptAccessExpectation: post("/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions")
    .params<ExemptAccessExpectationPath>({ "expectation_id": { style: "simple", explode: false }, "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_ledger_revision": number | string;
  "provider_subject_id": string | null;
  "rationale": string | null;
  "expires_at": string;
  "provider_entitlement_id"?: string | null;
}>())
    .returns(json<Portia3D403D282DFA1E4ED98B4578E4D7850CCEC17460A271A4EEE9124D80AD091488>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listAccessPopulations: get("/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-populations")
    .params<ListAccessPopulationsPath>({ "system_instance_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListAccessPopulationsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia9FCAF1CC602FB2404B5CF52AB3A7AF4FB9BF7AD634FD20E054362C2971D5C6E4>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTeams: get("/api/v1/tenants/{tenant_id}/teams")
    .params<ListTeamsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListTeamsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<PortiaEA6D5265BAC64B7B7ECBB403179F0802AEF5CDBFF6D32E0FCDF3EF54CB637D54>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTeam: get("/api/v1/tenants/{tenant_id}/teams/{team_id}")
    .params<GetTeamPath>({ "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia1EB0255AC1C1D800F4E43175A476BE2EEF9E1FBDC2D4430BCED070CC5D913FAD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  defineTeam: post("/api/v1/tenants/{tenant_id}/teams/{team_id}")
    .params<DefineTeamPath>({ "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "name": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  deleteTeam: del("/api/v1/tenants/{tenant_id}/teams/{team_id}")
    .params<DeleteTeamPath>({ "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTeamMembers: get("/api/v1/tenants/{tenant_id}/teams/{team_id}/members")
    .params<ListTeamMembersPath>({ "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListTeamMembersQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<Portia534841146AA01FC616B441EB6CC816411AE29E916AB93C8329D97EB96B560E0A>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignTeamMember: post("/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}")
    .params<AssignTeamMemberPath>({ "member_id": { style: "simple", explode: false }, "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  removeTeamMember: del("/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}")
    .params<RemoveTeamMemberPath>({ "member_id": { style: "simple", explode: false }, "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTeamRoles: get("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles")
    .params<ListTeamRolesPath>({ "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListTeamRolesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "search": { style: "form", explode: true }, "sort": { style: "form", explode: true } })
    .returns(json<Portia67FF42154696CE62730A90222AB6C1618C3883A7B83A530679D7947EF82A7536>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  assignTeamRole: post("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}")
    .params<AssignTeamRolePath>({ "role_id": { style: "simple", explode: false }, "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  removeTeamRole: del("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}")
    .params<RemoveTeamRolePath>({ "role_id": { style: "simple", explode: false }, "team_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTechnologyComponents: get("/api/v1/tenants/{tenant_id}/technology-components")
    .params<ListTechnologyComponentsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListTechnologyComponentsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaF91217E35959EE564AF2004EA1727032FC5084CF5CF0155ED5D422DB017C5B3F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordTechnologyComponent: post("/api/v1/tenants/{tenant_id}/technology-components")
    .params<RecordTechnologyComponentPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "category": string | null;
  "name": string | null;
  "owner_person_id": string;
  "environment_reference"?: string | null;
  "location_reference"?: string | null;
  "system_instance_id"?: string;
  "endpoint_count"?: number | string;
  "management_source"?: string | null;
}>())
    .returns(json<Portia63D54EA89C5C07D310D2BCA13AD5F7B74227B5C1A92D8305F29836A899A420D7>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getTechnologyComponent: get("/api/v1/tenants/{tenant_id}/technology-components/{component_id}")
    .params<GetTechnologyComponentPath>({ "component_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetTechnologyComponentQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia7B1AEE3985B856552CEAEAB86F86A8B046D256BB2EA8D4AB06B5F839FFA36B9F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseTechnologyComponent: put("/api/v1/tenants/{tenant_id}/technology-components/{component_id}")
    .params<ReviseTechnologyComponentPath>({ "component_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "name": string | null;
  "owner_person_id": string;
  "lifecycle": string | null;
  "environment_reference"?: string | null;
  "location_reference"?: string | null;
  "endpoint_count"?: number | string;
  "management_source"?: string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listTechnologyComponentRevisions: get("/api/v1/tenants/{tenant_id}/technology-components/{component_id}/revisions")
    .params<ListTechnologyComponentRevisionsPath>({ "component_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<ListTechnologyComponentRevisionsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "minimum_revision": { style: "form", explode: true } })
    .returns(json<PortiaF91217E35959EE564AF2004EA1727032FC5084CF5CF0155ED5D422DB017C5B3F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkDigestPreference: get("/api/v1/tenants/{tenant_id}/work-digest-preference")
    .params<GetWorkDigestPreferencePath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia1E516B7E59F08223EE49BB74695B06EE1AE7FE8D6BF8E50F76EE68386F7A72DA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  setWorkDigestPreference: put("/api/v1/tenants/{tenant_id}/work-digest-preference")
    .params<SetWorkDigestPreferencePath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "email_digest_enabled": boolean;
}>())
    .returns(json<Portia1E516B7E59F08223EE49BB74695B06EE1AE7FE8D6BF8E50F76EE68386F7A72DA>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWorkRelationships: get("/api/v1/tenants/{tenant_id}/work-relationships")
    .params<ListWorkRelationshipsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListWorkRelationshipsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia474D872CD82992E0F6DAA877E95A738CD0E651D58C89FD20650936F7E9442787>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  recordWorkRelationship: post("/api/v1/tenants/{tenant_id}/work-relationships")
    .params<RecordWorkRelationshipPath>({ "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "person_id": string;
  "source_worker_id": string | null;
  "worker_type": string | null;
  "lifecycle_status": string | null;
  "start_date": string;
  "end_date"?: string;
  "department"?: string | null;
  "manager_person_id"?: string;
  "sponsor_person_id"?: string;
}>())
    .returns(json<Portia36D1608FA46982FB7C0145375EB90B733846AA018C6B495E9A88F5B790185F37>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkRelationship: get("/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}")
    .params<GetWorkRelationshipPath>({ "relationship_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .query<GetWorkRelationshipQuery>({ "minimum_revision": { style: "form", explode: true } })
    .returns(json<Portia2AED3AF47093A366AD687BFFDD638FD60E8E83EBFD47EB5EB710621485B5041F>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reviseWorkRelationship: put("/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}")
    .params<ReviseWorkRelationshipPath>({ "relationship_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "expected_revision": number | string;
  "worker_type": string | null;
  "lifecycle_status": string | null;
  "start_date": string;
  "end_date"?: string;
  "department"?: string | null;
  "manager_person_id"?: string;
  "sponsor_person_id"?: string;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWorkforceObservations: get("/api/v1/tenants/{tenant_id}/workforce-observations")
    .params<ListWorkforceObservationsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListWorkforceObservationsQuery>({ "cursor": { style: "form", explode: true }, "kind": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaFB363E63265F366744A1615DFD1F101B4EADC47287216F1A34FA3DACB525E2AF>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  resolveWorkforceObservation: put("/api/v1/tenants/{tenant_id}/workforce-observations/{observation_id}/resolution")
    .params<ResolveWorkforceObservationPath>({ "observation_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "resolution": string | null;
  "note": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWorkforceReconciliationObservations: get("/api/v1/tenants/{tenant_id}/workforce-reconciliation-observations")
    .params<ListWorkforceReconciliationObservationsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListWorkforceReconciliationObservationsQuery>({ "cursor": { style: "form", explode: true }, "kind": { style: "form", explode: true }, "limit": { style: "form", explode: true }, "status": { style: "form", explode: true } })
    .returns(json<Portia7C156CFCACA80768D1A08D2432719D7F1C4D591F3979C08C55586944821D80F5>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkforceRosterSnapshotAsOf: get("/api/v1/tenants/{tenant_id}/workforce-roster-snapshot-as-of")
    .params<GetWorkforceRosterSnapshotAsOfPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<GetWorkforceRosterSnapshotAsOfQuery>({ "as_of": { style: "form", explode: true } })
    .returns(json<Portia03ACFAAE67D35BA82DFA61EDE278D3779AC79928770C69B153337B1F788534A3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listWorkforceRosterSnapshots: get("/api/v1/tenants/{tenant_id}/workforce-roster-snapshots")
    .params<ListWorkforceRosterSnapshotsPath>({ "tenant_id": { style: "simple", explode: false } })
    .query<ListWorkforceRosterSnapshotsQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<Portia96BF44EECD956DB8397701FC811AF70DE0D6E1ECDB8AB95318C6F10318CD1EDD>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  freezeWorkforceRosterSnapshot: post("/api/v1/tenants/{tenant_id}/workforce-roster-snapshots")
    .params<FreezeWorkforceRosterSnapshotPath>({ "tenant_id": { style: "simple", explode: false } })
    .returns(json<PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getWorkforceRosterSnapshot: get("/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}")
    .params<GetWorkforceRosterSnapshotPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia03ACFAAE67D35BA82DFA61EDE278D3779AC79928770C69B153337B1F788534A3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  amendWorkforceRosterSnapshot: post("/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/amendments")
    .params<AmendWorkforceRosterSnapshotPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .body(json<{
  "reason": string | null;
}>())
    .returns(json<PortiaAD1A36FB0E7DA3A7661197E93F9C3FAC072DFA215C0D19CDBA14BC399E425EC3>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  regenerateWorkforceRosterSnapshotManifest: get("/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration")
    .params<RegenerateWorkforceRosterSnapshotManifestPath>({ "snapshot_id": { style: "simple", explode: false }, "tenant_id": { style: "simple", explode: false } })
    .returns(json<Portia7289719EC72DE6F96317B0AF7168E28E848A0049DF1FA90D4E65F4328B1EC586>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  listEmailAddresses: get("/api/v1/users/{user_id}/email-addresses")
    .params<ListEmailAddressesPath>({ "user_id": { style: "simple", explode: false } })
    .query<ListEmailAddressesQuery>({ "cursor": { style: "form", explode: true }, "limit": { style: "form", explode: true } })
    .returns(json<PortiaEFE869FF2C47D1239F76963FA0AB021FCF0B3A5037DC8506F1AE7FF602338979>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEmailAddress: get("/api/v1/users/{user_id}/email-addresses/{email_address}")
    .params<GetEmailAddressPath>({ "email_address": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(json<Portia2D5AB432C63465374C50AF5FA1724B74CECAE9F5CB78FC5BE2C2AF45228FF552>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  reserveEmail: post("/api/v1/users/{user_id}/email-addresses/{email_address}")
    .params<ReserveEmailPath>({ "email_address": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  issueEmailChallenge: post("/api/v1/users/{user_id}/email-addresses/{email_address}/challenges")
    .params<IssueEmailChallengePath>({ "email_address": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  getEmailChallengeStatus: get("/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status")
    .params<GetEmailChallengeStatusPath>({ "email_address": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .returns(json<Portia022A384CC13FCCBBBDACFD95B82E4D9EE161732C5FF61B3FF97C70AF11E4D94C>())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
  completeEmailChallenge: post("/api/v1/users/{user_id}/email-addresses/{email_address}/verifications")
    .params<CompleteEmailChallengePath>({ "email_address": { style: "simple", explode: false }, "user_id": { style: "simple", explode: false } })
    .body(json<{
  "token": string | null;
}>())
    .returns(204, empty())
    .errors({ "400": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "401": empty(), "403": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "404": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "409": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "413": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "415": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>(), "500": json<{
  "type": string;
  "title": string;
  "status": number;
  "detail": string;
  "instance": string;
  "transient"?: boolean;
}>() }),
}, {
  "servers": [],
  "securitySchemes": {}
});

export const createApiClient = (options?: ClientOptions) => createClient(api, options);
