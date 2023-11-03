# rx-template-counterpartiescleaning
Шаблон разработки "Механизм очистки дублей контрагентов" для Directum RX. 
## Описание
Шаблон разработки предназначен для поиска и удаления дублей справочника "Организации", а также их замены на оригинал в договорных документах. 
Дубль - запись справочника Организации, свойства ИНН и КПП которой уже используются в другой записи этого справочника.

**Состав шаблона разработки:**
1.	Фоновый процесс - Очистка дублей. 01. Поиск дублей организаций.
2.	Фоновый процесс - Очистка дублей. 02. Замена организаций-дублей в документах.
3.	Фоновый процесс - Очистка дублей. 03. Удаление дублей организаций.

Все ФП настраиваются на периодический последовательный запуск.
## Порядок установки
Для работы требуется установленный Directum RX версии 4.6 и выше.
1. Склонировать репозиторий [https://github.com/DirectumCompany/rx-template-counterpartiescleaning.git](https://github.com/DirectumCompany/rx-template-counterpartiescleaning) в папку.
2. Указать в _ConfigSettings.xml DDS:
```xml
<block name="REPOSITORIES">
  <repository folderName="Base" solutionType="Base" url="" /> 
  <repository folderName="<Папка из п.1>" solutionType="Work" 
     url="https://github.com/DirectumCompany/rx-util-importdata-net-core.git" />
</block>
```
## Дополнительно
Инструкция для администратора: [Шаблон разработки Механизм очистки дублей контрагентов. Инструкция для администратора.docx](https://github.com/DirectumCompany/rx-template-counterpartiescleaning/blob/master/docs/%D0%A8%D0%B0%D0%B1%D0%BB%D0%BE%D0%BD%20%D1%80%D0%B0%D0%B7%D1%80%D0%B0%D0%B1%D0%BE%D1%82%D0%BA%D0%B8%20%D0%9C%D0%B5%D1%85%D0%B0%D0%BD%D0%B8%D0%B7%D0%BC%20%D0%BE%D1%87%D0%B8%D1%81%D1%82%D0%BA%D0%B8%20%D0%B4%D1%83%D0%B1%D0%BB%D0%B5%D0%B9%20%D0%BA%D0%BE%D0%BD%D1%82%D1%80%D0%B0%D0%B3%D0%B5%D0%BD%D1%82%D0%BE%D0%B2.%20%D0%98%D0%BD%D1%81%D1%82%D1%80%D1%83%D0%BA%D1%86%D0%B8%D1%8F%20%D0%B4%D0%BB%D1%8F%20%D0%B0%D0%B4%D0%BC%D0%B8%D0%BD%D0%B8%D1%81%D1%82%D1%80%D0%B0%D1%82%D0%BE%D1%80%D0%B0.docx)
