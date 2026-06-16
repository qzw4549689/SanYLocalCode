# 科法斯 countryCode_list 数据字典

**文件名**: 科法斯countryCode_list数据字典.xlsx
**日期**: 2026/5/22
**说明**: Coface ICON平台国家编码对照表（ISO-2字母编码）

---

## 国家编码列表

| countryCode | countryName |
|:---|:---|
| AD | Andorra |
| AE | United Arab Emirates |
| AF | Afghanistan |
| AG | Antigua and Barbuda |
| AI | Anguilla |
| AL | Albania |
| AM | Armenia |
| AN | Netherlands Antilles |
| AO | Angola |
| AQ | Antarctica |
| AR | Argentina |
| AS | American Samoa |
| AT | Austria |
| AU | Australia |
| AW | Aruba |
| AX | Aland Islands |
| AZ | Azerbaijan |
| BA | Bosnia and Herzegovina |
| BB | Barbados |
| BD | Bangladesh |
| BE | Belgium |
| BF | Burkina Faso |
| BG | Bulgaria |
| BH | Bahrain |
| BI | Burundi |
| BJ | Benin |
| BL | Saint Barthelemy |
| BM | Bermuda |
| BN | Brunei Darussalam |
| BO | Bolivia |
| BQ | Bonaire, Sint Eustatius and Saba |
| BR | Brazil |
| BS | Bahamas |
| BT | Bhutan |
| BV | Bouvet Islands |
| BW | Botswana |
| BY | Belarus |
| BZ | Belize |
| CA | Canada |
| CC | Cocos (Keeling) Islands |
| CD | Congo, the Democratic Republic of the |
| CF | Central African Republic |
| CG | Congo |
| CH | Switzerland |
| CI | Cote d'Ivoire |
| CK | Cook Islands |
| CL | Chile |
| CM | Cameroon |
| CN | China |
| CO | Colombia |
| CR | Costa Rica |
| CU | Cuba |
| CV | Cape Verde |

---

## 开发要点

1. **countryCode** 是Coface接口的入参之一，使用ISO-2字母编码
2. 三一CRM中客户表的**国家编码(mcs_countrycode)** 需要与此字典对应
3. 调用Coface接口时，需将客户表的国家编码转换为Coface的countryCode格式
4. 注意：部分国家名称可能有多种写法，需确认映射关系
