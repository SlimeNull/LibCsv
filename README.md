# CSV Helper

Easy helpers for csv writing and reading.

## Usage

Create a model class.

```cs
class MyData
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int Age { get; set; }
}
```

Create a file for writing.

```cs
using var textFile = File.CreateText("some_data.csv");
using var csvWriter = new CsvWriter<MyData>();

csvWriter.Write(new MyData()
{
    Name = "John",
    Description = "Software Developer",
    Age = 26,
});
```
